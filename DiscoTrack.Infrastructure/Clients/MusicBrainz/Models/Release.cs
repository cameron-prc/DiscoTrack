namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

public sealed record Release
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string? Status { get; init; }
	public string? Date { get; init; }
	public IReadOnlyList<Medium>? Media { get; init; }
	public ReleaseGroup? ReleaseGroup { get; init; }
	public IReadOnlyList<ArtistCredit>? ArtistCredit { get; init; }
}
