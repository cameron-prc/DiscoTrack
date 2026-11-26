namespace DiscoTrack.Infrastructure.Clients.Spotify.Models;

public sealed record PlayHistory
{
	public required Track Track { get; init; }
	public required DateTimeOffset PlayedAt { get; init; }
}
