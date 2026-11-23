using DiscoTrack.Albums;

namespace DiscoTrack.Discography;

public interface IDiscographyAlbumResolver
{
	Task<DiscographyAlbum?> Resolve(Album album, CancellationToken ct);
}
