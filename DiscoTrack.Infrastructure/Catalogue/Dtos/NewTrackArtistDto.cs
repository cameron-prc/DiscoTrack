using DiscoTrack.Artists;
using DiscoTrack.Tracks;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class NewTrackArtistDto
{
	public TrackId TrackId { get; init; }
	public ArtistId ArtistId { get; init; }
	public int Position { get; init; }
}
