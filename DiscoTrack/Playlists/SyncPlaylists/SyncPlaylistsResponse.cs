namespace DiscoTrack.Playlists.SyncPlaylists;

public record SyncPlaylistsResponse(IReadOnlyList<Playlist> Playlists, IReadOnlyList<Playlist> RemovedPlaylists);
