using DiscoTrack.Common;
using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;
using DiscoTrack.Core.Catalogue;
using DiscoTrack.Core.ExternalIds;

namespace DiscoTrack.Core.Library.SyncLibrary;

public class SyncLibrary(ICatalogue catalogue, ILibrarySource librarySource)
{
	public async Task<Either<IList<ISyncLibraryError>, SyncLibraryResponse>> Run(CancellationToken ct)
	{
		IReadOnlyList<Artist> knownArtists;
		IReadOnlyList<Album> knownAlbums;
		IReadOnlyList<LibraryAlbum> libraryAlbums;

		var newCatalogueArtists = new List<Artist>();
		var newCatalogueAlbums = new List<Album>();
		var failedToProcess = new List<LibraryAlbum>();

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

		var artistsById = IndexByExternalId(knownArtists, artist => artist.ExternalIds);
		var albumsById = IndexByExternalId(knownAlbums, album => album.ExternalIds);

		var newLibraryArtists = libraryAlbums
			.SelectMany(album => album.Artists)
			.Where(artist => !artistsById.ContainsKey(artist.Id))
			.DistinctBy(artist => artist.Id)
			.ToList();

		foreach (var newLibraryArtist in newLibraryArtists)
		{
			var artist = new Artist
			{
				Name = newLibraryArtist.Name,
				ExternalIds = ExternalIdsFor(newLibraryArtist.Id, newLibraryArtist.MusicBrainzId, artistsById),
			};

			newCatalogueArtists.Add(artist);
			foreach (var externalId in artist.ExternalIds)
			{
				artistsById[externalId] = artist;
			}
		}

		var newLibraryAlbums = libraryAlbums
			.Where(album => !albumsById.ContainsKey(album.Id))
			.DistinctBy(album => album.Id)
			.ToList();

		foreach (var newLibraryAlbum in newLibraryAlbums)
		{
			try
			{
				var albumArtists = newLibraryAlbum.Artists
					.Select((newLibraryAlbumArtist, index) => new AlbumArtist { Artist = artistsById[newLibraryAlbumArtist.Id], Position = index + 1 })
					.ToList();

				var album = new Album
				{
					Title = newLibraryAlbum.Title,
					Year = newLibraryAlbum.Year!.Value, // Not technically required by subsonic but required by us
					ExternalIds = ExternalIdsFor(newLibraryAlbum.Id, newLibraryAlbum.MusicBrainzId, albumsById),
					IsCompilation = newLibraryAlbum.IsCompilation,
					Artists = albumArtists
				};

				newCatalogueAlbums.Add(album);
				foreach (var externalId in album.ExternalIds)
				{
					albumsById[externalId] = album;
				}
			}
			catch (Exception)
			{
				failedToProcess.Add(newLibraryAlbum);
			}
		}

		try
		{
			await catalogue.AddArtists(newCatalogueArtists, ct);
			await catalogue.AddAlbums(newCatalogueAlbums, ct);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncLibraryError> { new FailedToSaveCatalogueError(exception.Message) };
		}

		return new SyncLibraryResponse(newCatalogueArtists, newCatalogueAlbums, failedToProcess);
	}

	private static Dictionary<ExternalId, T> IndexByExternalId<T>(IEnumerable<T> entities, Func<T, IEnumerable<ExternalId>> externalIds)
	{
		return entities
			.SelectMany(entity => externalIds(entity).Select(externalId => (ExternalId: externalId, Entity: entity)))
			.ToDictionary(pair => pair.ExternalId, pair => pair.Entity);
	}

	private static IReadOnlyList<ExternalId> ExternalIdsFor<T>(ExternalId sourceId, MusicBrainzId? musicBrainzId, IReadOnlyDictionary<ExternalId, T> taken)
	{
		if (musicBrainzId is null || taken.ContainsKey(musicBrainzId))
		{
			return [sourceId];
		}

		return [sourceId, musicBrainzId];
	}
}
