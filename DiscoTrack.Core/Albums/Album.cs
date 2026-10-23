using DiscoTrack.Core.Artists;

namespace DiscoTrack.Core.Albums;

public class Album
{
	private readonly List<AlbumArtist> _artists = [];

	public required string Title { get; set; }
	public required int Year { get; set; }
	public bool IsCompilation { get; set; }
	public IReadOnlyList<AlbumArtist> Artists => _artists;

	public void AddArtist(Artist artist)
	{
		if (_artists.Any(a => a.Artist == artist))
			return;

		_artists.Add(new AlbumArtist { Artist = artist, Position = _artists.Count + 1 });

		if (artist is ArtistFull full)
			full.AddAlbum(this);
	}

	public Artist? PrimaryArtist()
		=> IsCompilation ? null : _artists.OrderBy(a => a.Position).FirstOrDefault()?.Artist;
}
