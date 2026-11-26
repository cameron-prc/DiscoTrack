namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

public sealed record Medium
{
	public int Position { get; init; }
	public int TrackCount { get; init; }
	public IReadOnlyList<Track>? Tracks { get; init; }
}
