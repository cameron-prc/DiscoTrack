using DiscoTrack.Core.Artists;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class ArtistExternalIdDto
{
	public ArtistId ArtistId { get; init; }
	public required string Provider { get; init; }
	public required string Value { get; init; }
}
