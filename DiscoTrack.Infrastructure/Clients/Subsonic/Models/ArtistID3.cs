namespace DiscoTrack.Infrastructure.Clients.Subsonic.Models;

public sealed record ArtistID3
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public int? AlbumCount { get; init; }
	public string? MusicBrainzId { get; init; }
}
