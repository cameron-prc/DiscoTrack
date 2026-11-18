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

		var newLibraryArtists = libraryAlbums
			.SelectMany(album => album.Artists)
			.Where(artist => !artistsLookup.ContainsKey(artist.Id))
			.DistinctBy(artist => artist.Id)
			.ToList();

		var newArtists = ImportArtists(newLibraryArtists, artistsLookup);
		newArtists.Apply(artist => artist.ExternalIds.Apply(externalId => artistsLookup[externalId] = artist));

		var newLibraryAlbums = libraryAlbums
			.Where(album => !albumsLookup.ContainsKey(album.Id))
			.DistinctBy(album => album.Id)
			.ToList();

		var albumImports = ImportAlbums(newLibraryAlbums, albumsLookup, artistsLookup);
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
		var artistGroups = libraryArtists
			.GroupBy(libraryArtist => libraryArtist.MusicBrainzId ?? libraryArtist.Id)
			.ToList();

		var matchedArtists = artistGroups
			.Where(group => artistLookup.ContainsKey(group.Key))
			.Select(group => (LibraryArtist: group.First(), Artist: artistLookup[group.Key]))
			.Apply(match => logger.LogDebug("Importing {LibraryArtist} into existing artist {Artist}", match.LibraryArtist, match.Artist))
			.Apply(match => match.Artist.SetExternalId(match.LibraryArtist.Id))
			.Select(match => match.Artist);

		var newArtists = artistGroups
			.Where(group => !artistLookup.ContainsKey(group.Key))
			.Select(group => (LibraryArtist: group.First(), Artist: BuildArtist(group.First())))
			.Apply(match => logger.LogDebug("Importing {LibraryArtist} into new artist {Artist}", match.LibraryArtist, match.Artist))
			.Select(match => match.Artist);

		return matchedArtists.Concat(newArtists).ToList();
	}

	private IReadOnlyList<Either<SyncFailure, Album>> ImportAlbums(
		IReadOnlyList<LibraryAlbum> libraryAlbums,
		IReadOnlyDictionary<ExternalId, Album> albumLookup,
		IReadOnlyDictionary<ExternalId, Artist> artistsLookup)
	{
		var albumGroups = libraryAlbums
			.GroupBy(libraryAlbum => libraryAlbum.MusicBrainzId ?? libraryAlbum.Id)
			.ToList();

		var matchedAlbums = albumGroups
			.Where(group => albumLookup.ContainsKey(group.Key))
			.Select(group => (LibraryAlbum: group.First(), Album: albumLookup[group.Key]))
			.Apply(match => logger.LogDebug("Importing {LibraryAlbum} into existing album {Album}", match.LibraryAlbum, match.Album))
			.Apply(match => match.Album.SetExternalId(match.LibraryAlbum.Id))
			.Select(match => Either<SyncFailure, Album>.Right(match.Album));

		var newAlbums = albumGroups
			.Where(group => !albumLookup.ContainsKey(group.Key))
			.Select(group => BuildAlbum(group.First(), artistsLookup));

		return matchedAlbums.Concat(newAlbums).ToList();
	}

	private static Artist BuildArtist(LibraryArtist libraryArtist)
	{
		return new Artist
		{
			Name = libraryArtist.Name,
			ExternalIds = libraryArtist.ExternalIds,
		};
	}

	private static Artist FindArtist(LibraryArtist libraryArtist, IReadOnlyDictionary<ExternalId, Artist> artistsByExternalId)
	{
		return artistsByExternalId.GetValueOrDefault(libraryArtist.Id)
			?? artistsByExternalId[libraryArtist.MusicBrainzId ?? libraryArtist.Id];
	}

	private Either<SyncFailure, Album> BuildAlbum(LibraryAlbum libraryAlbum, IReadOnlyDictionary<ExternalId, Artist> artistsByExternalId)
	{
		try
		{
			var albumArtists = libraryAlbum.Artists
				.Select((libraryAlbumArtist, index) => new AlbumArtist { Artist = FindArtist(libraryAlbumArtist, artistsByExternalId), Position = index + 1 })
				.ToList();

			var album = new Album
			{
				Title = libraryAlbum.Title,
				Year = libraryAlbum.Year!.Value, // Not technically required by subsonic but required by us
				ExternalIds = libraryAlbum.ExternalIds,
				IsCompilation = libraryAlbum.IsCompilation,
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
