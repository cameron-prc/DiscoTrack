namespace DiscoTrack.Infrastructure.Clients.Spotify.Models;

public sealed record TrackExternalIds
{
	public string? Isrc { get; init; }
}
