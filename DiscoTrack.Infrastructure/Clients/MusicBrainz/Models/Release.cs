namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

public sealed record Release
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public ReleaseGroup? ReleaseGroup { get; init; }
	public IReadOnlyList<ArtistCredit>? ArtistCredit { get; init; }
}
