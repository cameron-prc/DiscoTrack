namespace DiscoTrack.Infrastructure.Clients.Spotify.Models;

public sealed record Track
{
	public string? Id { get; init; }
	public required string Name { get; init; }

	/// <summary>"track" for a song, "episode" for a podcast episode.</summary>
	public string? Type { get; init; }
	public int? DurationMs { get; init; }
	public int? DiscNumber { get; init; }
	public int? TrackNumber { get; init; }
	public TrackExternalIds? ExternalIds { get; init; }
	public IReadOnlyList<Artist>? Artists { get; init; }
	public Album? Album { get; init; }
}
