using DiscoTrack.Artists;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class TrackArtistDto
{
	public int CreditPosition { get; init; }
	public ArtistId ArtistId { get; init; }
	public required string Name { get; init; }
	public bool Tracked { get; init; }
}
