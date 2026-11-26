using DiscoTrack.Albums;
using DiscoTrack.Tracks;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class TrackDto
{
	public TrackId Id { get; init; }
	public AlbumId AlbumId { get; init; }
	public required string Title { get; init; }
	public int DiscNumber { get; init; }
	public int Position { get; init; }
	public int? DurationMs { get; init; }
}
