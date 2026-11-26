namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

public sealed record Recording
{
	public required string Id { get; init; }
	public int? Length { get; init; }
	public IReadOnlyList<ArtistCredit>? ArtistCredit { get; init; }
}
