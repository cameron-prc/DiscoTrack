using DiscoTrack.Playlists;

namespace DiscoTrack.Jobs.JobHandlers;

public interface ISyncPlaylistJobHandler
{
	Task Execute(PlaylistId playlistId, CancellationToken ct);
}
