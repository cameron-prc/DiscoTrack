using DiscoTrack.Artists;
using DiscoTrack.Common;

namespace DiscoTrack.Discography.SyncArtistWithDiscography;

public interface ISyncArtistWithDiscographyService
{
	Task<Either<IList<ISyncArtistWithDiscographyError>, SyncArtistWithDiscographyResponse>> Run(ArtistId artistId, CancellationToken cancellationToken);
}
