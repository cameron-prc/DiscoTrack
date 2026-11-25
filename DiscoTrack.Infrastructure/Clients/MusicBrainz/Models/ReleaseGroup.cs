namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

public sealed record ReleaseGroup
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string? FirstReleaseDate { get; init; }
	public string? PrimaryType { get; init; }
	public IReadOnlyList<string>? SecondaryTypes { get; init; }
	public IReadOnlyList<ArtistCredit>? ArtistCredit { get; init; }
	public IReadOnlyList<Release>? Releases { get; init; }
}
