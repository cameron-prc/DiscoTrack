namespace DiscoTrack.Core.Models;

public class AlbumArtist
{
	public Artist Artist { get; set; } = null!;
	public int Position { get; set; }   // 1 = primary
}
