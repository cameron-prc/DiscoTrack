using DiscoTrack.Tracks;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class TrackExternalIdDto
{
	public TrackId TrackId { get; init; }
	public required string Provider { get; init; }
	public required string Value { get; init; }
}
