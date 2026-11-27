namespace DiscoTrack.Playlists;

public interface IPlaylists
{
	Task<IReadOnlyList<Playlist>> GetPlaylists(CancellationToken ct);
	Task<Playlist?> GetPlaylist(PlaylistId playlistId, CancellationToken ct);
	Task SavePlaylist(Playlist playlist, CancellationToken ct);
	Task<IReadOnlyList<PlaylistItem>> GetItems(PlaylistId playlistId, CancellationToken ct);

	/// <summary>Makes the given items the playlist's only items.</summary>
	Task ReplaceItems(PlaylistId playlistId, IReadOnlyList<PlaylistItem> items, CancellationToken ct);
}
