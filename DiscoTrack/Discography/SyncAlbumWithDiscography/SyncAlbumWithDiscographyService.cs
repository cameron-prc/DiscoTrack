using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.Tracks;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Discography.SyncAlbumWithDiscography;

public class SyncAlbumWithDiscographyService(IDiscographySource source, IDiscographyAlbumResolver resolver, ICatalogue catalogue, ILogger<SyncAlbumWithDiscographyService> logger) : ISyncAlbumWithDiscographyService
{
	public async Task<Either<IList<ISyncAlbumWithDiscographyError>, SyncAlbumWithDiscographyResponse>> Run(AlbumId albumId, CancellationToken cancellationToken)
	{
		Album? album;
		IReadOnlyList<Artist> knownArtists;
		IReadOnlyList<Album> knownAlbums;
		DiscographyAlbum? discographyAlbum;

		try
		{
			album = await catalogue.GetAlbum(albumId, cancellationToken);
			knownArtists = await catalogue.GetArtists(cancellationToken);
			knownAlbums = await catalogue.GetAlbums(cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncAlbumWithDiscographyError> { new UnableToConnectToCatalogueError(exception.Message) };
		}

		if (album is null)
		{
			return new List<ISyncAlbumWithDiscographyError> { new UnableToFindAlbumError() };
		}

		try
		{
			discographyAlbum = await resolver.Resolve(album, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncAlbumWithDiscographyError> { new UnableToConnectToDiscographyError(exception.Message) };
		}

		if (discographyAlbum is null)
		{
			return new List<ISyncAlbumWithDiscographyError> { new UnableToFindDiscographyAlbumError() };
		}

		var artistsLookup = knownArtists
				.SelectMany(knownArtist => knownArtist.ExternalIds
						.Where(externalId => externalId.Provider == source.ArtistProvider)
						.Select(externalId => (ExternalId: externalId, Artist: knownArtist)))
				.ToDictionary(pair => pair.ExternalId, pair => pair.Artist);

		var creditedArtists = discographyAlbum.Artists
				.DistinctBy(discographyArtist => discographyArtist.Id)
				.ToList();

		var unidentifiedAlbumArtists = album.Artists
				.Select(albumArtist => albumArtist.Artist)
				.Where(artist => artist.ExternalIds.All(externalId => externalId.Provider != source.ArtistProvider))
				.DistinctBy(artist => MatchKey.For(artist.Name))
				.ToDictionary(artist => MatchKey.For(artist.Name));

		var updatedArtists = creditedArtists
				.Where(discographyArtist => !artistsLookup.ContainsKey(discographyArtist.Id))
				.DistinctBy(discographyArtist => MatchKey.For(discographyArtist.Name))
				.Where(discographyArtist => unidentifiedAlbumArtists.ContainsKey(MatchKey.For(discographyArtist.Name)))
				.Select(discographyArtist => (DiscographyArtist: discographyArtist, Artist: unidentifiedAlbumArtists[MatchKey.For(discographyArtist.Name)]))
				.Apply(match => match.Artist.SetExternalId(match.DiscographyArtist.Id))
				.Apply(match => artistsLookup[match.DiscographyArtist.Id] = match.Artist)
				.Select(match => match.Artist)
				.ToList();

		var newArtists = creditedArtists
				.Where(discographyArtist => !artistsLookup.ContainsKey(discographyArtist.Id))
				.Select(BuildArtist)
				.Apply(artist => artist.ExternalIds.Apply(externalId => artistsLookup[externalId] = artist));

		var absorbedAlbum = knownAlbums
				.FirstOrDefault(knownAlbum => knownAlbum.Id != album.Id && knownAlbum.ExternalIds.Contains(discographyAlbum.ReleaseGroupId));

		var syncedAlbum = new Album
		{
			Id = album.Id,
			Title = discographyAlbum.Title,
			Year = discographyAlbum.Year ?? album.Year,
			IsCompilation = discographyAlbum.IsCompilation,
			Tracked = album.Tracked || absorbedAlbum is { Tracked: true },
			ExternalIds = album.ExternalIds
						.Concat(absorbedAlbum?.ExternalIds ?? [])
						.Where(externalId => externalId.Provider != source.ReleaseGroupProvider)
						.Distinct()
						.Append(discographyAlbum.ReleaseGroupId)
						.ToList(),
			Artists = creditedArtists
						.Select((discographyArtist, index) => new AlbumArtist { Artist = artistsLookup[discographyArtist.Id], Position = index + 1 })
						.ToList(),
		};

		var previousArtistIds = album.Artists.Select(albumArtist => albumArtist.Artist.Id).ToHashSet();
		var syncedArtistIds = syncedAlbum.Artists.Select(albumArtist => albumArtist.Artist.Id).ToHashSet();

		var linkedArtists = syncedAlbum.Artists
				.Select(albumArtist => albumArtist.Artist)
				.Where(artist => !previousArtistIds.Contains(artist.Id))
				.ToList();
		var unlinkedArtists = album.Artists
				.Select(albumArtist => albumArtist.Artist)
				.Where(artist => !syncedArtistIds.Contains(artist.Id))
				.ToList();

		try
		{
			foreach (var artist in newArtists.Concat(updatedArtists))
			{
				await catalogue.SaveArtist(artist, cancellationToken);
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save the artists credited on {Album} by discography album {ReleaseGroupId}", syncedAlbum, discographyAlbum.ReleaseGroupId);

			return new List<ISyncAlbumWithDiscographyError> { new UnableToSaveArtistError(exception.Message) };
		}

		try
		{
			if (absorbedAlbum is null)
			{
				await catalogue.SaveAlbum(syncedAlbum, cancellationToken);
			}
			else
			{
				var tracks = await catalogue.GetAlbumTracks(album.Id, cancellationToken);
				var absorbedTracks = await catalogue.GetAlbumTracks(absorbedAlbum.Id, cancellationToken);

				await catalogue.MergeAlbums(syncedAlbum, absorbedAlbum.Id, absorbedTracks.MergeInto(syncedAlbum.Id, tracks), cancellationToken);
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save {Album} after syncing it with discography album {ReleaseGroupId}", syncedAlbum, discographyAlbum.ReleaseGroupId);

			return new List<ISyncAlbumWithDiscographyError> { new UnableToSaveAlbumError(exception.Message) };
		}

		return new SyncAlbumWithDiscographyResponse(syncedAlbum, newArtists, updatedArtists, linkedArtists, unlinkedArtists, absorbedAlbum);
	}

	private static Artist BuildArtist(DiscographyArtist discographyArtist)
	{
		return new Artist
		{
			Name = discographyArtist.Name,
			ExternalIds = [discographyArtist.Id],
		};
	}
}
