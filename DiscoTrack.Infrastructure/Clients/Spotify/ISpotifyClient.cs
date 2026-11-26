using DiscoTrack.Infrastructure.Clients.Spotify.Models;

namespace DiscoTrack.Infrastructure.Clients.Spotify;

public interface ISpotifyClient
{
	Task<IReadOnlyList<PlayHistory>> GetRecentlyPlayed(int limit, long? afterUnixMilliseconds, CancellationToken ct);
}
