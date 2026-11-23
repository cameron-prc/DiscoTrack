using DiscoTrack.Albums;
using DiscoTrack.Common;

namespace DiscoTrack.Discography.SyncAlbumWithDiscography;

public interface ISyncAlbumWithDiscographyService
{
	Task<Either<IList<ISyncAlbumWithDiscographyError>, SyncAlbumWithDiscographyResponse>> Run(AlbumId albumId, CancellationToken cancellationToken);
}
