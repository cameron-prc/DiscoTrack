using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Catalogue;

public interface ICatalogue
{
	Task<Artist?> GetArtist(ArtistId artistId, CancellationToken ct);
	Task<IReadOnlyList<Artist>> GetArtists(CancellationToken ct);
	Task<IReadOnlyList<Album>> GetAlbums(CancellationToken ct);
	Task<Album?> GetAlbum(AlbumId albumId, CancellationToken ct);
	Task<IReadOnlyList<Album>> GetArtistAlbums(ArtistId artistId, CancellationToken ct);
	Task<IReadOnlyList<Artist>> GetAlbumArtists(AlbumId albumId, CancellationToken ct);
	Task SaveArtist(Artist artist, CancellationToken ct);
	Task SaveAlbum(Album album, CancellationToken ct);
}
