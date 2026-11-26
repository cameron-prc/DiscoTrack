using DiscoTrack.Artists;

namespace DiscoTrack.Tracks;

public class TrackArtist
{
	public Artist Artist { get; set; } = null!;
	public int Position { get; set; }   // 1 = primary
}
