using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.Discography.SyncAlbumWithDiscography;
using DiscoTrack.ExternalIds;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Discography.SyncArtistWithDiscography;

public class SyncArtistWithDiscographyService(
	IDiscographySource source,
	IDiscographyArtistResolver resolver,
	ISyncAlbumWithDiscographyService syncAlbumWithDiscography,
	ICatalogue catalogue,
	TimeProvider timeProvider,
	ILogger<SyncArtistWithDiscographyService> logger) : ISyncArtistWithDiscographyService
{
	/// <summary>
	/// <para>Syncs a catalogue artist and its albums with the discography, then adds the albums the catalogue is missing.</para>
	/// <para>
	/// First resolve the discography artist, save its id and name on the artist and track the artist
	/// Second sync each of the artist's albums with its discography album
	/// Third add each discography album no catalogue album holds as a new untracked album
	/// Fourth save the time of the scan on the artist against the discography's artist provider
	/// </para>
	/// </summary>
	public async Task<Either<IList<ISyncArtistWithDiscographyError>, SyncArtistWithDiscographyResponse>> Run(ArtistId artistId, CancellationToken cancellationToken)
	{
		Artist? artist;
		IReadOnlyList<Album> artistAlbums;
		DiscographyArtist? discographyArtist;
		IReadOnlyList<DiscographyAlbum> discographyAlbums;
		IReadOnlyList<Album> knownAlbums;
		IReadOnlyList<Artist> knownArtists;

		try
		{
			artist = await catalogue.GetArtist(artistId, cancellationToken);
			artistAlbums = await catalogue.GetArtistAlbums(artistId, cancellationToken);
			knownArtists = await catalogue.GetArtists(cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncArtistWithDiscographyError> { new UnableToConnectToCatalogueError(exception.Message) };
		}

		if (artist is null)
		{
			return new List<ISyncArtistWithDiscographyError> { new UnableToFindArtistError() };
		}

		try
		{
			discographyArtist = await resolver.Resolve(artist, artistAlbums, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncArtistWithDiscographyError> { new UnableToConnectToDiscographyError(exception.Message) };
		}

		if (discographyArtist is null)
		{
			return new List<ISyncArtistWithDiscographyError> { new UnableToFindDiscographyArtistError() };
		}

		if (discographyArtist.Id == source.VariousArtistsId)
		{
			return new List<ISyncArtistWithDiscographyError> { new UnableToSyncVariousArtistsError() };
		}

		var absorbedArtist = knownArtists
				.FirstOrDefault(knownArtist => knownArtist.Id != artist.Id && knownArtist.ExternalIds.Contains(discographyArtist.Id));

		if (absorbedArtist is not null || !artist.ExternalIds.Contains(discographyArtist.Id) || artist.Name != discographyArtist.Name || artist.Disambiguation != discographyArtist.Disambiguation || !artist.Tracked)
		{
			artist.Tracked = true;
			artist.SetExternalId(discographyArtist.Id);
			artist.SetExternalIds(artist.ExternalIds.Concat(absorbedArtist?.ExternalIds ?? []).Distinct().ToList());
			artist.Name = discographyArtist.Name;
			artist.Disambiguation = discographyArtist.Disambiguation;

			try
			{
				if (absorbedArtist is null)
				{
					await catalogue.SaveArtist(artist, cancellationToken);
				}
				else
				{
					await catalogue.MergeArtists(artist, absorbedArtist.Id, cancellationToken);
				}
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				logger.LogWarning(exception, "Failed to save {Artist} after syncing it with discography artist {DiscographyArtistId}", artist, discographyArtist.Id);

				return new List<ISyncArtistWithDiscographyError> { new UnableToSaveArtistError(exception.Message) };
			}
		}

		var syncedAlbums = new List<Album>();
		var unsyncedAlbums = new List<Album>();
		var absorbedAlbumIds = new HashSet<AlbumId>();

		foreach (var album in artistAlbums)
		{
			if (absorbedAlbumIds.Contains(album.Id))
			{
				continue;
			}

			var result = await syncAlbumWithDiscography.Run(album.Id, cancellationToken);

			if (result.IsLeft)
			{
				logger.LogWarning("Failed to sync {Album} with the discography: {Errors}", album, result.GetLeft);
				unsyncedAlbums.Add(album);
			}
			else
			{
				syncedAlbums.Add(result.GetRight.Album);

				if (result.GetRight.AbsorbedAlbum is { } absorbedAlbum)
				{
					absorbedAlbumIds.Add(absorbedAlbum.Id);
					syncedAlbums.RemoveAll(syncedAlbum => syncedAlbum.Id == absorbedAlbum.Id);
				}
			}
		}

		try
		{
			discographyAlbums = await source.GetArtistAlbums(discographyArtist.Id, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncArtistWithDiscographyError> { new UnableToConnectToDiscographyError(exception.Message) };
		}

		try
		{
			knownAlbums = await catalogue.GetAlbums(cancellationToken);
			knownArtists = await catalogue.GetArtists(cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncArtistWithDiscographyError> { new UnableToConnectToCatalogueError(exception.Message) };
		}

		var knownReleaseGroupIds = knownAlbums
				.SelectMany(album => album.ExternalIds)
				.Where(externalId => externalId.Provider == source.ReleaseGroupProvider)
				.ToHashSet();

		var unidentifiedTitleKeys = knownAlbums
				.Where(album => album.Artists.Any(albumArtist => albumArtist.Artist.Id == artistId))
				.Where(album => album.ExternalIds.All(externalId => externalId.Provider != source.ReleaseGroupProvider))
				.Select(album => MatchKey.For(album.Title))
				.ToHashSet();

		var missingAlbums = discographyAlbums
				.DistinctBy(discographyAlbum => discographyAlbum.ReleaseGroupId)
				.Where(discographyAlbum => !knownReleaseGroupIds.Contains(discographyAlbum.ReleaseGroupId))
				.ToList();

		var skippedAlbums = missingAlbums
				.Where(discographyAlbum => discographyAlbum.Year is null || unidentifiedTitleKeys.Contains(MatchKey.For(discographyAlbum.Title)))
				.ToList();

		var newDiscographyAlbums = missingAlbums.Except(skippedAlbums).ToList();

		var artistsLookup = knownArtists
				.SelectMany(knownArtist => knownArtist.ExternalIds
						.Where(externalId => externalId.Provider == source.ArtistProvider)
						.Select(externalId => (ExternalId: externalId, Artist: knownArtist)))
				.ToDictionary(pair => pair.ExternalId, pair => pair.Artist);

		var newArtists = newDiscographyAlbums
				.SelectMany(discographyAlbum => discographyAlbum.Artists)
				.DistinctBy(discographyArtist => discographyArtist.Id)
				.Where(discographyArtist => !artistsLookup.ContainsKey(discographyArtist.Id))
				.Select(BuildArtist)
				.Apply(newArtist => newArtist.ExternalIds.Apply(externalId => artistsLookup[externalId] = newArtist));

		var newAlbums = newDiscographyAlbums
				.Select(discographyAlbum => BuildAlbum(discographyAlbum, artistsLookup))
				.ToList();

		try
		{
			foreach (var newArtist in newArtists)
			{
				await catalogue.SaveArtist(newArtist, cancellationToken);
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save the artists credited on the discography albums of {Artist}", artist);

			return new List<ISyncArtistWithDiscographyError> { new UnableToSaveArtistError(exception.Message) };
		}

		try
		{
			foreach (var newAlbum in newAlbums)
			{
				await catalogue.SaveAlbum(newAlbum, cancellationToken);
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save the discography albums of {Artist}", artist);

			return new List<ISyncArtistWithDiscographyError> { new UnableToSaveAlbumError(exception.Message) };
		}

		artist.SetLastScannedAt(source.ArtistProvider, timeProvider.GetUtcNow());

		try
		{
			await catalogue.SaveArtist(artist, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save the time {Artist} was scanned", artist);

			return new List<ISyncArtistWithDiscographyError> { new UnableToSaveArtistError(exception.Message) };
		}

		return new SyncArtistWithDiscographyResponse(artist, syncedAlbums, unsyncedAlbums, newAlbums, newArtists, skippedAlbums, absorbedArtist);
	}

	private static Artist BuildArtist(DiscographyArtist discographyArtist)
	{
		return new Artist
		{
			Name = discographyArtist.Name,
			Disambiguation = discographyArtist.Disambiguation,
			ExternalIds = [discographyArtist.Id],
		};
	}

	private static Album BuildAlbum(DiscographyAlbum discographyAlbum, IReadOnlyDictionary<ExternalId, Artist> artistsLookup)
	{
		return new Album
		{
			Title = discographyAlbum.Title,
			Year = discographyAlbum.Year!.Value,
			IsCompilation = discographyAlbum.IsCompilation,
			Tracked = false,
			ExternalIds = [discographyAlbum.ReleaseGroupId],
			Artists = discographyAlbum.Artists
					.DistinctBy(discographyArtist => discographyArtist.Id)
					.Select((discographyArtist, index) => new AlbumArtist { Artist = artistsLookup[discographyArtist.Id], Position = index + 1 })
					.ToList(),
		};
	}
}
