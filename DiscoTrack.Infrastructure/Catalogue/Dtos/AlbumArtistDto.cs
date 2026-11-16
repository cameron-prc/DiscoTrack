using DiscoTrack.Core.Artists;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class AlbumArtistDto
{
	public ArtistId ArtistId { get; init; }
	public int Position { get; init; }
	public required string Name { get; init; }
}
