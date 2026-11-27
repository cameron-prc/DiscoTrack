using DiscoTrack.Common;

namespace DiscoTrack.Playlists.SyncPlaylists;

public interface ISyncPlaylistsService
{
	Task<Either<IList<ISyncPlaylistsError>, SyncPlaylistsResponse>> Run(CancellationToken cancellationToken);
}
