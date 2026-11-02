using DiscoTrack.Core.Artists;

namespace DiscoTrack.Core.Albums;

public class Album
{
	public string? SubsonicId { get; set; }
	public required string Title { get; set; }
	public required int Year { get; set; }
	public bool IsCompilation { get; set; }
	public IReadOnlyList<AlbumArtist> Artists { get; set; } = [];

	public Artist? PrimaryArtist()
		=> IsCompilation ? null : Artists.MinBy(a => a.Position)?.Artist;
}
