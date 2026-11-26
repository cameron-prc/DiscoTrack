using DiscoTrack.Artists;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class ArtistDto
{
	public ArtistId Id { get; init; }
	public required string Name { get; init; }
	public bool Tracked { get; init; }
}
