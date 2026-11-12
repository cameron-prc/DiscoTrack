using DiscoTrack.Common;
using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;
using DiscoTrack.Core.Catalogue;

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

		var artistsDict = knownArtists.Where(artist => artist.SubsonicId != null).ToDictionary(knownArtist => knownArtist.SubsonicId!);
		var albumDict = knownAlbums.Where(album => album.SubsonicId != null).ToDictionary(knownAlbum => knownAlbum.SubsonicId!);

		var newLibraryArtists = libraryAlbums.SelectMany(album => album.Artists).Where(artist => !artistsDict.ContainsKey(artist.SubsonicId)).DistinctBy(artist => artist.SubsonicId);

		newCatalogueArtists = newLibraryArtists.Select(newLibraryArtist => new Artist { Name = newLibraryArtist.Name, SubsonicId = newLibraryArtist.SubsonicId }).ToList();

		foreach (var artist in newCatalogueArtists)
		{
			artistsDict[artist.SubsonicId!] = artist;
		}

		var newLibraryAlbums = libraryAlbums.Where(album => !albumDict.ContainsKey(album.SubsonicId)).DistinctBy(album => album.SubsonicId);

		foreach (var newLibraryAlbum in newLibraryAlbums)
		{
			try
			{
				var albumArtists = newLibraryAlbum.Artists
					.Select((newLibraryAlbumArtist, index) => new AlbumArtist { Artist = artistsDict[newLibraryAlbumArtist.SubsonicId], Position = index + 1 })
					.ToList();

				var album = new Album
				{
					Title = newLibraryAlbum.Title,
					Year = newLibraryAlbum.Year!.Value, // Not technically required by subsonic but required by us
					SubsonicId = newLibraryAlbum.SubsonicId,
					IsCompilation = newLibraryAlbum.IsCompilation,
					Artists = albumArtists
				};

				newCatalogueAlbums.Add(album);
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
}
