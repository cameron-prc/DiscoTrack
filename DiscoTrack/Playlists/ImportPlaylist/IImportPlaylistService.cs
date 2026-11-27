using DiscoTrack.Common;

namespace DiscoTrack.Playlists.ImportPlaylist;

public interface IImportPlaylistService
{
	Task<Either<IList<IImportPlaylistError>, ImportPlaylistResponse>> Run(PlaylistId playlistId, bool onlyWhenChanged, CancellationToken cancellationToken);
}
