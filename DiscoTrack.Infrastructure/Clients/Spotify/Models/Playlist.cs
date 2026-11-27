namespace DiscoTrack.Infrastructure.Clients.Spotify.Models;

public sealed record Playlist
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public string? Description { get; init; }
	public string? SnapshotId { get; init; }
	public PlaylistOwner? Owner { get; init; }
	public PlaylistTracks? Tracks { get; init; }
}

public sealed record PlaylistOwner
{
	public string? Id { get; init; }
	public string? DisplayName { get; init; }
}

public sealed record PlaylistTracks
{
	public int Total { get; init; }
}
