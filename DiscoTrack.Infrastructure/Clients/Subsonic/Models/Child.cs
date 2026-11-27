namespace DiscoTrack.Infrastructure.Clients.Subsonic.Models;

public sealed record Child
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string? AlbumId { get; init; }
	public int? Track { get; init; }
	public int? DiscNumber { get; init; }
	public int? Duration { get; init; }
	public string? ArtistId { get; init; }
	public string? Artist { get; init; }
	public IReadOnlyList<ArtistID3>? Artists { get; init; }
	public string? MusicBrainzId { get; init; }
	public IReadOnlyList<string>? Isrc { get; init; }
}
