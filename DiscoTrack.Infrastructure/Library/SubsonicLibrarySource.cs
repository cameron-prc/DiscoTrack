using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Clients.Subsonic.Models;
using DiscoTrack.Library;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Library;

public class SubsonicLibrarySource(ISubsonicClient client, IOptions<SubsonicOptions> options) : ILibrarySource
{
	private const string EverySong = "\"\"";

	private readonly int _pageSize = options.Value.PageSize;

	public Provider Provider
	{
		get { return Provider.Subsonic; }
	}

	public async Task<IReadOnlyList<LibraryAlbum>> GetAlbums(CancellationToken ct)
	{
		var albums = new List<LibraryAlbum>();
		IReadOnlyList<AlbumID3> albumPage;

		do
		{
			albumPage = await client.GetAlbumList2(AlbumListType.AlphabeticalByName, _pageSize, albums.Count, ct);
			albums.AddRange(albumPage.Select(a => a.ToLibraryAlbum()));
		}
		while (albumPage.Count >= _pageSize);

		return albums;
	}

	public async Task<IReadOnlyList<LibraryArtist>> GetArtists(CancellationToken ct)
	{
		var indexes = await client.GetArtists(ct);

		return indexes
			.SelectMany(index => index.Artist)
			.Select(a => a.ToLibraryArtist())
			.ToList();
	}

	public async Task<IReadOnlyList<LibraryTrack>> GetTracks(CancellationToken ct)
	{
		var tracks = new List<LibraryTrack>();
		var offset = 0;
		IReadOnlyList<Child> songPage;

		do
		{
			songPage = await client.Search3(EverySong, _pageSize, offset, ct);
			offset += songPage.Count;
			tracks.AddRange(songPage
				.Where(song => !string.IsNullOrEmpty(song.AlbumId))
				.Select(song => song.ToLibraryTrack(song.AlbumId!)));
		}
		while (songPage.Count >= _pageSize);

		return tracks;
	}
}
