using DiscoTrack.Common;

namespace DiscoTrack.Playlists.AddPlaylist;

public interface IAddPlaylistService
{
	Task<Either<IList<IAddPlaylistError>, AddPlaylistResponse>> Run(string reference, CancellationToken cancellationToken);
}
