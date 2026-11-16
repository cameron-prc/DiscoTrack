namespace DiscoTrack.Infrastructure.Clients.Subsonic.Models;

public sealed record AlbumID3
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public int? Year { get; init; }
	public bool IsCompilation { get; init; }
	public string? ArtistId { get; init; }
	public string? Artist { get; init; }
	public IReadOnlyList<ArtistID3>? Artists { get; init; }
	public string? MusicBrainzId { get; init; }
}
