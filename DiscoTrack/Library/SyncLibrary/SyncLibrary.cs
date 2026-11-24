using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.ExternalIds;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Library.SyncLibrary;

public class SyncLibrary(ICatalogue catalogue, ILibrarySource librarySource, ILogger<SyncLibrary> logger)
{
	public async Task<Either<IList<ISyncLibraryError>, SyncLibraryResponse>> Run(CancellationToken ct)
	{
		IReadOnlyList<Artist> knownArtists;
		IReadOnlyList<Album> knownAlbums;
		IReadOnlyList<LibraryAlbum> libraryAlbums;

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
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncLibraryError> { new FailedToReadLibraryError(exception.Message) };
		}

		var artistsLookup = knownArtists
			.SelectMany(knownArtist => knownArtist.ExternalIds.Select(externalId => (ExternalId: externalId, Artist: knownArtist)))
			.ToDictionary(pair => pair.ExternalId, pair => pair.Artist);
		var albumsLookup = knownAlbums
			.SelectMany(knownAlbum => knownAlbum.ExternalIds.Select(externalId => (ExternalId: externalId, Album: knownAlbum)))
			.ToDictionary(pair => pair.ExternalId, pair => pair.Album);

		var libraryArtists = libraryAlbums
			.SelectMany(album => album.Artists)
			.DistinctBy(artist => artist.Id)
			.ToList();

		var newArtists = ImportArtists(libraryArtists, artistsLookup);
		newArtists.Apply(artist => artist.ExternalIds.Apply(externalId => artistsLookup[externalId] = artist));

		var distinctLibraryAlbums = libraryAlbums
			.DistinctBy(album => album.Id)
			.ToList();

		var albumImports = ImportAlbums(distinctLibraryAlbums, albumsLookup, artistsLookup);
		var newAlbums = albumImports.Where(import => import.IsRight).Select(import => import.GetRight).ToList();

		var artistSaves = await SaveArtists(newArtists, ct);
		var albumSaves = await SaveAlbums(newAlbums, ct);

		var savedArtists = artistSaves.Where(save => save.IsRight).Select(save => save.GetRight).ToList();
		var savedAlbums = albumSaves.Where(save => save.IsRight).Select(save => save.GetRight).ToList();
		var failures = albumImports.Where(import => import.IsLeft).Select(import => import.GetLeft)
			.Concat(artistSaves.Where(save => save.IsLeft).Select(save => save.GetLeft))
			.Concat(albumSaves.Where(save => save.IsLeft).Select(save => save.GetLeft))
			.ToList();

		return new SyncLibraryResponse(savedArtists, savedAlbums, failures);
	}

	private IReadOnlyList<Artist> ImportArtists(IReadOnlyList<LibraryArtist> libraryArtists, IReadOnlyDictionary<ExternalId, Artist> artistLookup)
	{
		var matches = libraryArtists
			.Select(libraryArtist => (LibraryArtist: libraryArtist, Artist: Find(libraryArtist.Id, libraryArtist.MusicBrainzId, artistLookup)))
			.ToList();

		var relinkedArtists = matches
			.Where(match => match.Artist is not null)
			.GroupBy(match => match.Artist!, match => match.LibraryArtist)
			.Select(group => (LibraryArtists: group.ToList(), Artist: group.Key, PreviousExternalIds: group.Key.ExternalIds))
			.Apply(match => match.Artist.SetExternalIds(match.LibraryArtists.Select(libraryArtist => libraryArtist.Id).ToList()))
			.Where(match => !match.PreviousExternalIds.SequenceEqual(match.Artist.ExternalIds))
			.Apply(match => logger.LogDebug("Importing {LibraryArtists} into existing artist {Artist}", match.LibraryArtists, match.Artist))
			.Select(match => match.Artist);

		var newArtists = matches
			.Where(match => match.Artist is null)
			.GroupBy(match => match.LibraryArtist.MusicBrainzId ?? match.LibraryArtist.Id, match => match.LibraryArtist)
			.Select(group => (LibraryArtists: group.ToList(), Artist: BuildArtist(group.ToList())))
			.Apply(match => logger.LogDebug("Importing {LibraryArtists} into new artist {Artist}", match.LibraryArtists, match.Artist))
			.Select(match => match.Artist);

		var orphanedArtists = artistLookup.Values
			.Except(matches.Select(match => match.Artist).OfType<Artist>())
			.Where(artist => artist.ExternalIds.Any(externalId => externalId.Provider == librarySource.Provider))
			.Apply(artist => logger.LogDebug("Removing library ids from {Artist} as it is no longer in the library", artist))
			.Apply(artist => artist.RemoveExternalIds(librarySource.Provider));

		return relinkedArtists.Concat(orphanedArtists).Concat(newArtists).ToList();
	}

	private IReadOnlyList<Either<SyncFailure, Album>> ImportAlbums(
		IReadOnlyList<LibraryAlbum> libraryAlbums,
		IReadOnlyDictionary<ExternalId, Album> albumLookup,
		IReadOnlyDictionary<ExternalId, Artist> artistsLookup)
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

	private static Artist BuildArtist(IReadOnlyList<LibraryArtist> libraryArtists)
	{
		return new Artist
		{
			Name = libraryArtists[0].Name,
			ExternalIds = libraryArtists.SelectMany(libraryArtist => libraryArtist.ExternalIds).Distinct().ToList(),
		};
	}

	private static Artist FindArtist(LibraryArtist libraryArtist, IReadOnlyDictionary<ExternalId, Artist> artistsByExternalId)
	{
		return artistsByExternalId.GetValueOrDefault(libraryArtist.Id)
			?? artistsByExternalId[libraryArtist.MusicBrainzId ?? libraryArtist.Id];
	}

	private Either<SyncFailure, Album> BuildAlbum(IReadOnlyList<LibraryAlbum> libraryAlbums, IReadOnlyDictionary<ExternalId, Artist> artistsByExternalId)
	{
		var libraryAlbum = libraryAlbums[0];

		try
		{
			var albumArtists = libraryAlbum.Artists
				.Select((libraryAlbumArtist, index) => new AlbumArtist { Artist = FindArtist(libraryAlbumArtist, artistsByExternalId), Position = index + 1 })
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
