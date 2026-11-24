using DiscoTrack.Albums;

namespace DiscoTrack.Infrastructure.Catalogue.Dtos;

internal sealed class AlbumDto
{
	public AlbumId Id { get; init; }
	public required string Title { get; init; }
	public int Year { get; init; }
	public bool IsCompilation { get; init; }
	public bool Tracked { get; init; }
}
