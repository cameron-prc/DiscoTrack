namespace DiscoTrack.Core.Models;

public class ArtistFull : Artist
{
	private readonly List<Album> _albums = [];

	public override IReadOnlyList<Album> Albums => _albums;

	public override void AddAlbum(Album album)
	{
		if (_albums.Contains(album))
			return;

		_albums.Add(album);
		album.AddArtist(this);
	}
}
