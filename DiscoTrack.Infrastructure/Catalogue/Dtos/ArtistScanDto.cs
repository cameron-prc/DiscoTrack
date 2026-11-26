using DiscoTrack.Artists;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class ArtistScanDto
{
	public ArtistId ArtistId { get; init; }
	public required string Provider { get; init; }
	public required string ScannedAt { get; init; }
}
