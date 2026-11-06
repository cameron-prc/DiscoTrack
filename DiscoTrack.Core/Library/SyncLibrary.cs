using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;
using DiscoTrack.Core.Catalogue;

namespace DiscoTrack.Core.Library;

public class SyncLibrary(ICatalogue catalogue, ILibrarySource librarySource)
{
	public async Task Run(CancellationToken ct)
	{
		var newCatalogueArtists = new List<Artist>();
		var newCatalogueAlbums = new List<Album>();

		// TODO: Handle failure
		var knownArtists = await catalogue.GetArtists(ct);
		var knownAlbums = await catalogue.GetAlbums(ct);

		var artistsDict = knownArtists.Where(artist => artist.SubsonicId != null).ToDictionary(knownArtist => knownArtist.SubsonicId!);
		var albumDict = knownAlbums.Where(album => album.SubsonicId != null).ToDictionary(knownAlbum => knownAlbum.SubsonicId!);

		// TODO: Handle failure
		var libraryAlbums = await librarySource.GetAlbums(ct);
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
					Year = newLibraryAlbum.Year!.Value,
					SubsonicId = newLibraryAlbum.SubsonicId,
					IsCompilation = newLibraryAlbum.IsCompilation,
					Artists = albumArtists
				};

				newCatalogueAlbums.Add(album);
			}
			catch (Exception exception)
			{
				// TODO: Add a proper logger
				Console.WriteLine($"Unable to create album {newLibraryAlbum.Title} ({newLibraryAlbum.Year})");
				Console.WriteLine(exception.Message);
			}
		}

		await catalogue.AddArtists(newCatalogueArtists, ct);
		await catalogue.AddAlbums(newCatalogueAlbums, ct);

		// TODO: Return a error or response union
	}
}
