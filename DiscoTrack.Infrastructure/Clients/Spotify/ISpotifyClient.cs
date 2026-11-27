using DiscoTrack.Infrastructure.Clients.Spotify.Models;

namespace DiscoTrack.Infrastructure.Clients.Spotify;

public interface ISpotifyClient
{
	Task<IReadOnlyList<PlayHistory>> GetRecentlyPlayed(int limit, long? afterUnixMilliseconds, CancellationToken ct);
	Task<Playlist?> GetPlaylist(string playlistId, CancellationToken ct);
	Task<Page<PlaylistItem>> GetPlaylistItems(string playlistId, int limit, int offset, CancellationToken ct);
}
