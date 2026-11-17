using DiscoTrack.Albums;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class AlbumExternalIdDto
{
	public AlbumId AlbumId { get; init; }
	public required string Provider { get; init; }
	public required string Value { get; init; }
}
