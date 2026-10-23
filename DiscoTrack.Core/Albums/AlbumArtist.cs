using DiscoTrack.Core.Artists;

namespace DiscoTrack.Core.Albums;

public class AlbumArtist
{
	public Artist Artist { get; set; } = null!;
	public int Position { get; set; }   // 1 = primary
}
