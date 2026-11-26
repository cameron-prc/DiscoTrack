namespace DiscoTrack.Infrastructure.Clients.Spotify.Models;

public sealed record Album
{
	public string? Id { get; init; }
	public required string Name { get; init; }
}
