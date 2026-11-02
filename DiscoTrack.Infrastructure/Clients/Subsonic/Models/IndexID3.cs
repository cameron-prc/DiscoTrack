namespace DiscoTrack.Infrastructure.Clients.Subsonic.Models;

public sealed record IndexID3
{
	public required string Name { get; init; }
	public IReadOnlyList<ArtistID3> Artist { get; init; } = [];
}
