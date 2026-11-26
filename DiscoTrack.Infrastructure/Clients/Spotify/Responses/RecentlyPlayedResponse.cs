using DiscoTrack.Infrastructure.Clients.Spotify.Models;

namespace DiscoTrack.Infrastructure.Clients.Spotify.Responses;

internal sealed class RecentlyPlayedResponse
{
	public IReadOnlyList<PlayHistory> Items { get; init; } = [];
}
