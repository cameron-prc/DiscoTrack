namespace DiscoTrack.Infrastructure.Clients.Spotify.Models;

public sealed record PlaylistItem
{
	public DateTimeOffset? AddedAt { get; init; }
	public bool IsLocal { get; init; }

	/// <summary>A song or a podcast episode; null when Spotify no longer has it.</summary>
	public Track? Track { get; init; }
}
