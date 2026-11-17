using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class NewAlbumArtistDto
{
	public AlbumId AlbumId { get; init; }
	public ArtistId ArtistId { get; init; }
	public int Position { get; init; }
}
