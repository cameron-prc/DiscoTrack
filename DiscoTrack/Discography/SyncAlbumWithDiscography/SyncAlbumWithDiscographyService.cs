using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.ExternalIds;
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
		IReadOnlyList<Track> knownTracks = [];
		IReadOnlyList<DiscographyTrack>? discographyTracks = null;

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

		var absorbedAlbum = knownAlbums
				.FirstOrDefault(knownAlbum => knownAlbum.Id != album.Id && knownAlbum.ExternalIds.Contains(discographyAlbum.ReleaseGroupId));
		var tracked = album.Tracked || absorbedAlbum is { Tracked: true };

		if (tracked)
		{
			try
			{
				var tracklist = await GetDiscographyTracks(album, absorbedAlbum, discographyAlbum, cancellationToken);

				discographyTracks = tracklist.Count > 0 ? tracklist : null;
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				return new List<ISyncAlbumWithDiscographyError> { new UnableToConnectToDiscographyError(exception.Message) };
			}
		}

		if (tracked || absorbedAlbum is not null)
		{
			try
			{
				knownTracks = await catalogue.GetAlbumTracks(album.Id, cancellationToken);

				if (absorbedAlbum is not null)
				{
					knownTracks = (await catalogue.GetAlbumTracks(absorbedAlbum.Id, cancellationToken)).MergeInto(album.Id, knownTracks);
				}
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				return new List<ISyncAlbumWithDiscographyError> { new UnableToConnectToCatalogueError(exception.Message) };
			}
		}

		var artistsLookup = knownArtists
				.SelectMany(knownArtist => knownArtist.ExternalIds
						.Where(externalId => externalId.Provider == source.ArtistProvider)
						.Select(externalId => (ExternalId: externalId, Artist: knownArtist)))
				.ToDictionary(pair => pair.ExternalId, pair => pair.Artist);

		var albumCredits = discographyAlbum.Artists
				.DistinctBy(discographyArtist => discographyArtist.Id)
				.ToList();
		var creditedArtists = albumCredits
				.Concat((discographyTracks ?? []).SelectMany(discographyTrack => discographyTrack.Artists))
				.DistinctBy(discographyArtist => discographyArtist.Id)
				.ToList();

		var unidentifiedAlbumArtists = album.Artists
				.Select(albumArtist => albumArtist.Artist)
				.Concat(knownTracks.SelectMany(track => track.Artists).Select(trackArtist => trackArtist.Artist))
				.Where(artist => artist.ExternalIds.All(externalId => externalId.Provider != source.ArtistProvider))
				.DistinctBy(artist => MatchKey.For(artist.Name))
				.ToDictionary(artist => MatchKey.For(artist.Name));

		var identifiedArtists = creditedArtists
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

		var describedArtists = creditedArtists
				.Select(discographyArtist => (DiscographyArtist: discographyArtist, Artist: artistsLookup[discographyArtist.Id]))
				.Where(match => match.Artist.Disambiguation != match.DiscographyArtist.Disambiguation)
				.Apply(match => match.Artist.Disambiguation = match.DiscographyArtist.Disambiguation)
				.Select(match => match.Artist);

		var updatedArtists = identifiedArtists
				.Concat(describedArtists)
				.Distinct()
				.ToList();

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
			Artists = albumCredits
						.Select((discographyArtist, index) => new AlbumArtist { Artist = artistsLookup[discographyArtist.Id], Position = index + 1 })
						.ToList(),
		};

		var syncedTracks = discographyTracks is null
				? knownTracks
				: SyncTracks(album.Id, knownTracks, discographyTracks, artistsLookup);

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

				if (discographyTracks is not null)
				{
					await catalogue.SaveAlbumTracks(syncedAlbum.Id, syncedTracks, cancellationToken);
				}
			}
			else
			{
				await catalogue.MergeAlbums(syncedAlbum, absorbedAlbum.Id, syncedTracks, cancellationToken);
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save {Album} after syncing it with discography album {ReleaseGroupId}", syncedAlbum, discographyAlbum.ReleaseGroupId);

			return new List<ISyncAlbumWithDiscographyError> { new UnableToSaveAlbumError(exception.Message) };
		}

		return new SyncAlbumWithDiscographyResponse(syncedAlbum, newArtists, updatedArtists, linkedArtists, unlinkedArtists, absorbedAlbum, discographyTracks is null ? null : syncedTracks);
	}

	// The tracklist of the release the album holds. An album holding several (two editions merged) takes the longest.
	private async Task<IReadOnlyList<DiscographyTrack>> GetDiscographyTracks(Album album, Album? absorbedAlbum, DiscographyAlbum discographyAlbum, CancellationToken cancellationToken)
	{
		var resolvedReleaseId = album.ExternalIds.FirstOrDefault(externalId => externalId.Provider == source.ReleaseProvider);
		var releaseIds = album.ExternalIds
				.Concat(absorbedAlbum?.ExternalIds ?? [])
				.Where(externalId => externalId.Provider == source.ReleaseProvider)
				.Distinct()
				.ToList();

		if (releaseIds.Count == 0)
		{
			return discographyAlbum.Tracks ?? await source.GetAlbumTracks(discographyAlbum.ReleaseGroupId, cancellationToken);
		}

		var tracklists = new List<IReadOnlyList<DiscographyTrack>>();

		foreach (var releaseId in releaseIds)
		{
			tracklists.Add(releaseId == resolvedReleaseId && discographyAlbum.Tracks is not null
					? discographyAlbum.Tracks
					: await source.GetAlbumTracks(releaseId, cancellationToken));
		}

		return tracklists.MaxBy(tracklist => tracklist.Count)!;
	}

	// Tracks the discography does not list stay only while another provider still knows them
	private IReadOnlyList<Track> SyncTracks(
		AlbumId albumId,
		IReadOnlyList<Track> knownTracks,
		IReadOnlyList<DiscographyTrack> discographyTracks,
		IReadOnlyDictionary<ExternalId, Artist> artistsLookup)
	{
		var unlistedTracks = knownTracks.ToList();
		var tracks = new List<Track>();

		foreach (var discographyTrack in discographyTracks)
		{
			var track = unlistedTracks.FindMatch([discographyTrack.RecordingId], discographyTrack.DiscNumber, discographyTrack.Position, discographyTrack.Title, anyRecording: true)
					?? new Track { AlbumId = albumId, Title = discographyTrack.Title, DiscNumber = discographyTrack.DiscNumber, Position = discographyTrack.Position };

			unlistedTracks.Remove(track);

			track.SetExternalIds([discographyTrack.RecordingId]);
			track.Title = discographyTrack.Title;
			track.DiscNumber = discographyTrack.DiscNumber;
			track.Position = discographyTrack.Position;
			track.DurationMs = discographyTrack.LengthMs ?? track.DurationMs;
			track.Artists = discographyTrack.Artists.Count == 0
					? track.Artists
					: discographyTrack.Artists
							.DistinctBy(discographyArtist => discographyArtist.Id)
							.Select((discographyArtist, index) => new TrackArtist { Artist = artistsLookup[discographyArtist.Id], Position = index + 1 })
							.ToList();

			tracks.Add(track);
		}

		return tracks
				.Concat(unlistedTracks.Where(track => track.ExternalIds.Any(externalId => externalId.Provider != source.RecordingProvider)))
				.ToList();
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
}
