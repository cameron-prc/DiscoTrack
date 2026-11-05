using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;

namespace DiscoTrack.Core.Catalogue;

public interface ICatalogue
{
	Task<IReadOnlyList<Artist>> GetArtists(CancellationToken ct);
	Task<IReadOnlyList<Album>> GetAlbums(CancellationToken ct);
	Task<IReadOnlyList<Album>> GetArtistAlbums(ArtistId artistId, CancellationToken ct);
	Task AddArtists(IReadOnlyList<Artist> artists, CancellationToken ct);
	Task AddAlbums(IReadOnlyList<Album> albums, CancellationToken ct);
}
