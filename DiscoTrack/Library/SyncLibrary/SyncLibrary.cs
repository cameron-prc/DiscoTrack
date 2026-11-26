using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Tracks;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Library.SyncLibrary;

public class SyncLibrary(ICatalogue catalogue, ILibrarySource librarySource, ILogger<SyncLibrary> logger)
{
	public async Task<Either<IList<ISyncLibraryError>, SyncLibraryResponse>> Run(CancellationToken ct)
	{
		IReadOnlyList<Artist> knownArtists;
		IReadOnlyList<Album> knownAlbums;
		IReadOnlyList<LibraryAlbum> libraryAlbums;
		IReadOnlyList<LibraryTrack> libraryTracks;

		try
		{
			knownArtists = await catalogue.GetArtists(ct);
			knownAlbums = await catalogue.GetAlbums(ct);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncLibraryError> { new FailedToReadCatalogueError(exception.Message) };
		}

		try
		{
			libraryAlbums = await librarySource.GetAlbums(ct);
			libraryTracks = await librarySource.GetTracks(ct);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncLibraryError> { new FailedToReadLibraryError(exception.Message) };
		}

		var knownArtistsLookup = ToLookup(knownArtists);
		var albumsLookup = knownAlbums
			.SelectMany(knownAlbum => knownAlbum.ExternalIds.Select(externalId => (ExternalId: externalId, Album: knownAlbum)))
			.ToDictionary(pair => pair.ExternalId, pair => pair.Album);

		var albumArtistIds = libraryAlbums
			.SelectMany(album => album.Artists)
			.Select(artist => artist.Id)
			.ToHashSet();
		var libraryArtists = libraryAlbums
			.SelectMany(album => album.Artists)
			.Concat(libraryTracks.SelectMany(track => track.Artists))
			.DistinctBy(artist => artist.Id)
			.ToList();

		var distinctLibraryAlbums = libraryAlbums
			.DistinctBy(album => album.Id)
			.ToList();

		var newArtists = ImportArtists(libraryArtists, albumArtistIds, knownArtistsLookup, FindCreditedNamesakes(distinctLibraryAlbums, albumsLookup, knownArtists));
		var artistsLookup = ToLookup(knownArtists.Concat(newArtists).Distinct());

		var previouslyOwnedAlbums = knownAlbums.Where(HoldsLibraryId).ToHashSet();

		var albumImports = ImportAlbums(distinctLibraryAlbums, albumsLookup, artistsLookup);
		var newAlbums = albumImports.Where(import => import.IsRight).Select(import => import.GetRight).ToList();

		var artistSaves = await SaveArtists(newArtists, ct);
		var albumSaves = await SaveAlbums(newAlbums, ct);

		var savedArtists = artistSaves.Where(save => save.IsRight).Select(save => save.GetRight).ToList();
		var savedAlbums = albumSaves.Where(save => save.IsRight).Select(save => save.GetRight).ToList();

		var unsavedAlbums = albumSaves.Where(save => save.IsLeft).Select(save => save.GetLeft.Entity).OfType<Album>().ToHashSet();
		var libraryTracksByAlbum = libraryTracks
			.DistinctBy(track => track.Id)
			.ToLookup(track => track.AlbumId);
		var albumsWithLibraryTracks = knownAlbums
			.Concat(newAlbums)
			.Distinct()
			.Where(album => HoldsLibraryId(album) || previouslyOwnedAlbums.Contains(album))
			.Where(album => !unsavedAlbums.Contains(album));

		var trackSaves = await SaveTracks(albumsWithLibraryTracks, libraryTracksByAlbum, artistsLookup, ct);

		var savedTracks = trackSaves.Where(save => save.IsRight).SelectMany(save => save.GetRight).ToList();
		var failures = albumImports.Where(import => import.IsLeft).Select(import => import.GetLeft)
			.Concat(artistSaves.Where(save => save.IsLeft).Select(save => save.GetLeft))
			.Concat(albumSaves.Where(save => save.IsLeft).Select(save => save.GetLeft))
			.Concat(trackSaves.Where(save => save.IsLeft).Select(save => save.GetLeft))
			.ToList();

		return new SyncLibraryResponse(savedArtists, savedAlbums, savedTracks, failures);
	}

	private bool HoldsLibraryId(Album album)
	{
		return album.ExternalIds.Any(externalId => externalId.Provider == librarySource.Provider);
	}

	// Several artists can hold one library id: the library merges artists that share a name
	private static ILookup<ExternalId, Artist> ToLookup(IEnumerable<Artist> artists)
	{
		return artists
			.OrderBy(artist => artist.Id.Value)
			.SelectMany(artist => artist.ExternalIds.Select(externalId => (ExternalId: externalId, Artist: artist)))
			.ToLookup(pair => pair.ExternalId, pair => pair.Artist);
	}

	// The artists a catalogue album credits under the same name as the library does, whichever ids they hold
	private static IReadOnlyList<(ExternalId LibraryArtistId, Artist Artist)> FindCreditedNamesakes(
		IReadOnlyList<LibraryAlbum> libraryAlbums,
		IReadOnlyDictionary<ExternalId, Album> albumLookup,
		IReadOnlyList<Artist> knownArtists)
	{
		var artistsById = knownArtists.ToDictionary(artist => artist.Id);

		return libraryAlbums
			.Select(libraryAlbum => (LibraryAlbum: libraryAlbum, Album: Find(libraryAlbum.Id, libraryAlbum.MusicBrainzId, albumLookup)))
			.Where(match => match.Album is not null)
			.SelectMany(match => match.LibraryAlbum.Artists.SelectMany(libraryArtist => match.Album!.Artists
				.Where(credit => MatchKey.For(credit.Artist.Name) == MatchKey.For(libraryArtist.Name))
				.Where(credit => artistsById.ContainsKey(credit.Artist.Id))
				.Select(credit => (LibraryArtistId: libraryArtist.Id, Artist: artistsById[credit.Artist.Id]))))
			.Distinct()
			.ToList();
	}

	private IReadOnlyList<Artist> ImportArtists(
		IReadOnlyList<LibraryArtist> libraryArtists,
		IReadOnlySet<ExternalId> albumArtistIds,
		ILookup<ExternalId, Artist> artistLookup,
		IReadOnlyList<(ExternalId LibraryArtistId, Artist Artist)> creditedNamesakes)
	{
		var libraryArtistsById = libraryArtists.ToDictionary(libraryArtist => libraryArtist.Id);

		var matches = libraryArtists
			.SelectMany(libraryArtist => FindArtists(libraryArtist, artistLookup).Select(artist => (LibraryArtist: libraryArtist, Artist: artist)))
			.Concat(creditedNamesakes.Select(namesake => (LibraryArtist: libraryArtistsById[namesake.LibraryArtistId], namesake.Artist)))
			.Distinct()
			.ToList();
		var matchedLibraryArtistIds = matches.Select(match => match.LibraryArtist.Id).ToHashSet();

		var relinkedArtists = matches
			.GroupBy(match => match.Artist, match => match.LibraryArtist)
			.Select(group => (LibraryArtists: group.ToList(), Artist: group.Key, PreviousExternalIds: group.Key.ExternalIds, PreviouslyTracked: group.Key.Tracked))
			.Apply(match => match.Artist.SetExternalIds(match.LibraryArtists.Select(libraryArtist => libraryArtist.Id).ToList()))
			.Apply(match => match.Artist.Tracked = match.Artist.Tracked || match.LibraryArtists.Any(libraryArtist => albumArtistIds.Contains(libraryArtist.Id)))
			.Where(match => !match.PreviousExternalIds.SequenceEqual(match.Artist.ExternalIds) || match.Artist.Tracked != match.PreviouslyTracked)
			.Apply(match => logger.LogDebug("Importing {LibraryArtists} into existing artist {Artist}", match.LibraryArtists, match.Artist))
			.Select(match => match.Artist);

		var newArtists = libraryArtists
			.Where(libraryArtist => !matchedLibraryArtistIds.Contains(libraryArtist.Id))
			.GroupBy(libraryArtist => libraryArtist.MusicBrainzId ?? libraryArtist.Id)
			.Select(group => (LibraryArtists: group.ToList(), Artist: BuildArtist(group.ToList(), albumArtistIds)))
			.Apply(match => logger.LogDebug("Importing {LibraryArtists} into new artist {Artist}", match.LibraryArtists, match.Artist))
			.Select(match => match.Artist);

		var orphanedArtists = artistLookup
			.SelectMany(holders => holders)
			.Distinct()
			.Except(matches.Select(match => match.Artist))
			.Where(artist => artist.ExternalIds.Any(externalId => externalId.Provider == librarySource.Provider))
			.Apply(artist => logger.LogDebug("Removing library ids from {Artist} as it is no longer in the library", artist))
			.Apply(artist => artist.RemoveExternalIds(librarySource.Provider));

		return relinkedArtists.Concat(orphanedArtists).Concat(newArtists).ToList();
	}

	private IReadOnlyList<Either<SyncFailure, Album>> ImportAlbums(
		IReadOnlyList<LibraryAlbum> libraryAlbums,
		IReadOnlyDictionary<ExternalId, Album> albumLookup,
		ILookup<ExternalId, Artist> artistsLookup)
	{
		var matches = libraryAlbums
			.Select(libraryAlbum => (LibraryAlbum: libraryAlbum, Album: Find(libraryAlbum.Id, libraryAlbum.MusicBrainzId, albumLookup)))
			.ToList();

		var relinkedAlbums = matches
			.Where(match => match.Album is not null)
			.GroupBy(match => match.Album!, match => match.LibraryAlbum)
			.Select(group => (LibraryAlbums: group.ToList(), Album: group.Key, PreviousExternalIds: group.Key.ExternalIds))
			.Apply(match => match.Album.SetExternalIds(match.LibraryAlbums.Select(libraryAlbum => libraryAlbum.Id).ToList()))
			.Where(match => !match.PreviousExternalIds.SequenceEqual(match.Album.ExternalIds))
			.Apply(match => logger.LogDebug("Importing {LibraryAlbums} into existing album {Album}", match.LibraryAlbums, match.Album))
			.Select(match => Either<SyncFailure, Album>.Right(match.Album));

		var newAlbums = matches
			.Where(match => match.Album is null)
			.GroupBy(match => match.LibraryAlbum.MusicBrainzId ?? match.LibraryAlbum.Id, match => match.LibraryAlbum)
			.Select(group => BuildAlbum(group.ToList(), artistsLookup));

		var orphanedAlbums = albumLookup.Values
			.Except(matches.Select(match => match.Album).OfType<Album>())
			.Where(album => album.ExternalIds.Any(externalId => externalId.Provider == librarySource.Provider))
			.Apply(album => logger.LogDebug("Removing library ids from {Album} as it is no longer in the library", album))
			.Apply(album => album.RemoveExternalIds(librarySource.Provider))
			.Select(Either<SyncFailure, Album>.Right);

		return relinkedAlbums.Concat(orphanedAlbums).Concat(newAlbums).ToList();
	}

	private static T? Find<T>(ExternalId id, ExternalId? musicBrainzId, IReadOnlyDictionary<ExternalId, T> lookup) where T : class
	{
		return lookup.GetValueOrDefault(id)
			?? (musicBrainzId is null ? null : lookup.GetValueOrDefault(musicBrainzId));
	}

	private static Artist BuildArtist(IReadOnlyList<LibraryArtist> libraryArtists, IReadOnlySet<ExternalId> albumArtistIds)
	{
		return new Artist
		{
			Name = libraryArtists[0].Name,
			Tracked = libraryArtists.Any(libraryArtist => albumArtistIds.Contains(libraryArtist.Id)),
			ExternalIds = libraryArtists.SelectMany(libraryArtist => libraryArtist.ExternalIds).Distinct().ToList(),
		};
	}

	private static IReadOnlyList<Artist> FindArtists(LibraryArtist libraryArtist, ILookup<ExternalId, Artist> artistsByExternalId)
	{
		var holders = artistsByExternalId[libraryArtist.Id].ToList();

		return holders.Count > 0 || libraryArtist.MusicBrainzId is null
			? holders
			: artistsByExternalId[libraryArtist.MusicBrainzId].ToList();
	}

	// Prefers the artist the album already credits, then the one the catalogue has held longest
	private static Artist ChooseArtist(LibraryArtist libraryArtist, IReadOnlyList<AlbumArtist> albumArtists, ILookup<ExternalId, Artist> artistsByExternalId)
	{
		var creditedArtistIds = albumArtists.Select(albumArtist => albumArtist.Artist.Id).ToHashSet();
		var holders = FindArtists(libraryArtist, artistsByExternalId);

		return holders.FirstOrDefault(artist => creditedArtistIds.Contains(artist.Id))
			?? holders.First();
	}

	private Either<SyncFailure, Album> BuildAlbum(IReadOnlyList<LibraryAlbum> libraryAlbums, ILookup<ExternalId, Artist> artistsByExternalId)
	{
		var libraryAlbum = libraryAlbums[0];

		try
		{
			var albumArtists = libraryAlbum.Artists
				.Select((libraryAlbumArtist, index) => new AlbumArtist { Artist = ChooseArtist(libraryAlbumArtist, [], artistsByExternalId), Position = index + 1 })
				.ToList();

			var album = new Album
			{
				Title = libraryAlbum.Title,
				Year = libraryAlbum.Year!.Value, // Not technically required by subsonic but required by us
				ExternalIds = libraryAlbums.SelectMany(duplicate => duplicate.ExternalIds).Distinct().ToList(),
				IsCompilation = libraryAlbum.IsCompilation,
				Tracked = true,
				Artists = albumArtists
			};

			logger.LogDebug("Importing {LibraryAlbum} into new album {Album}", libraryAlbum, album);

			return album;
		}
		catch (Exception exception)
		{
			logger.LogWarning(exception, "Failed to import album {Album}", libraryAlbum);

			return new SyncFailure(SyncAction.Import, libraryAlbum, exception.Message);
		}
	}

	private IReadOnlyList<Track> ImportTracks(
		Album album,
		IReadOnlyList<Track> knownTracks,
		IReadOnlyList<LibraryTrack> libraryTracks,
		ILookup<ExternalId, Artist> artistsByExternalId)
	{
		var tracks = knownTracks.ToList();
		var libraryIds = new Dictionary<Track, List<ExternalId>>();

		foreach (var libraryTrack in libraryTracks)
		{
			var track = tracks.FindMatch(libraryTrack.ExternalIds, libraryTrack.DiscNumber, libraryTrack.TrackNumber, libraryTrack.Title);

			if (track is null)
			{
				track = BuildTrack(album, libraryTrack, artistsByExternalId);
				tracks.Add(track);
			}

			libraryIds.TryAdd(track, []);
			libraryIds[track].Add(libraryTrack.Id);
		}

		tracks
			.Where(track => libraryIds.ContainsKey(track))
			.Apply(track => track.SetExternalIds(libraryIds[track]));
		tracks
			.Where(track => !libraryIds.ContainsKey(track))
			.Apply(track => track.RemoveExternalIds(librarySource.Provider));

		return tracks;
	}

	private static Track BuildTrack(Album album, LibraryTrack libraryTrack, ILookup<ExternalId, Artist> artistsByExternalId)
	{
		return new Track
		{
			AlbumId = album.Id,
			Title = libraryTrack.Title,
			DiscNumber = libraryTrack.DiscNumber,
			Position = libraryTrack.TrackNumber,
			DurationMs = libraryTrack.DurationSeconds * 1000,
			ExternalIds = libraryTrack.ExternalIds,
			Artists = libraryTrack.Artists
				.Select(libraryArtist => ChooseArtist(libraryArtist, album.Artists, artistsByExternalId))
				.Distinct()
				.Select((artist, index) => new TrackArtist { Artist = artist, Position = index + 1 })
				.ToList(),
		};
	}

	private async Task<IReadOnlyList<Either<SyncFailure, IReadOnlyList<Track>>>> SaveTracks(
		IEnumerable<Album> albums,
		ILookup<ExternalId, LibraryTrack> libraryTracksByAlbum,
		ILookup<ExternalId, Artist> artistsByExternalId,
		CancellationToken ct)
	{
		var saves = new List<Either<SyncFailure, IReadOnlyList<Track>>>();

		foreach (var album in albums)
		{
			saves.Add(await SaveAlbumTracks(album, libraryTracksByAlbum, artistsByExternalId, ct));
		}

		return saves;
	}

	private async Task<Either<SyncFailure, IReadOnlyList<Track>>> SaveAlbumTracks(
		Album album,
		ILookup<ExternalId, LibraryTrack> libraryTracksByAlbum,
		ILookup<ExternalId, Artist> artistsByExternalId,
		CancellationToken ct)
	{
		try
		{
			var knownTracks = await catalogue.GetAlbumTracks(album.Id, ct);
			var previousExternalIds = knownTracks.ToDictionary(track => track.Id, track => track.ExternalIds);
			var libraryTracks = album.ExternalIds
				.Where(externalId => externalId.Provider == librarySource.Provider)
				.SelectMany(externalId => libraryTracksByAlbum[externalId])
				.ToList();

			var tracks = ImportTracks(album, knownTracks, libraryTracks, artistsByExternalId);
			var changedTracks = tracks
				.Where(track => !previousExternalIds.TryGetValue(track.Id, out var externalIds) || !externalIds.SequenceEqual(track.ExternalIds))
				.ToList();

			if (changedTracks.Count > 0)
			{
				logger.LogDebug("Importing {TrackCount} tracks into {Album}", changedTracks.Count, album);

				await catalogue.SaveAlbumTracks(album.Id, tracks, ct);
			}

			return Either<SyncFailure, IReadOnlyList<Track>>.Right(changedTracks);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to import the tracks of {Album}", album);

			return new SyncFailure(SyncAction.Save, album, exception.Message);
		}
	}

	private async Task<IReadOnlyList<Either<SyncFailure, Artist>>> SaveArtists(IEnumerable<Artist> artists, CancellationToken ct)
	{
		var saves = new List<Either<SyncFailure, Artist>>();

		foreach (var artist in artists)
		{
			saves.Add(await SaveArtist(artist, ct));
		}

		return saves;
	}

	private async Task<Either<SyncFailure, Artist>> SaveArtist(Artist artist, CancellationToken ct)
	{
		try
		{
			await catalogue.SaveArtist(artist, ct);

			return artist;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to import artist {Artist}", artist);

			return new SyncFailure(SyncAction.Save, artist, exception.Message);
		}
	}

	private async Task<IReadOnlyList<Either<SyncFailure, Album>>> SaveAlbums(IEnumerable<Album> albums, CancellationToken ct)
	{
		var saves = new List<Either<SyncFailure, Album>>();

		foreach (var album in albums)
		{
			saves.Add(await SaveAlbum(album, ct));
		}

		return saves;
	}

	private async Task<Either<SyncFailure, Album>> SaveAlbum(Album album, CancellationToken ct)
	{
		try
		{
			await catalogue.SaveAlbum(album, ct);

			return album;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to import album {Album}", album);

			return new SyncFailure(SyncAction.Save, album, exception.Message);
		}
	}
}
