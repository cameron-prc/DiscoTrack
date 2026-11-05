using DiscoTrack.Core.Library;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Clients.Subsonic.Models;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Library;

public class SubsonicLibrarySource(ISubsonicClient client, IOptions<SubsonicOptions> options) : ILibrarySource
{
	private readonly int _pageSize = options.Value.PageSize;

	public async Task<IReadOnlyList<LibraryAlbum>> GetAlbums(CancellationToken? ct = null)
	{
		var token = ct ?? CancellationToken.None;
		var albums = new List<LibraryAlbum>();
		IReadOnlyList<AlbumID3> albumPage;

		do
		{
			albumPage = await client.GetAlbumList2(AlbumListType.AlphabeticalByName, _pageSize, albums.Count, token);
			albums.AddRange(albumPage.Select(a => a.ToLibraryAlbum()));
		}
		while (albumPage.Count >= _pageSize);

		return albums;
	}

	public async Task<IReadOnlyList<LibraryArtist>> GetArtists(CancellationToken? ct = null)
	{
		var indexes = await client.GetArtists(ct ?? CancellationToken.None);

		return indexes
			.SelectMany(index => index.Artist)
			.Select(a => a.ToLibraryArtist())
			.ToList();
	}
}
