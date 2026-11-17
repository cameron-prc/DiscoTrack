using DiscoTrack.Artists;

namespace DiscoTrack.Albums;

public class AlbumArtist
{
	public Artist Artist { get; set; } = null!;
	public int Position { get; set; }   // 1 = primary
}
