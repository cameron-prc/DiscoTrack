using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Clients.Spotify.Models;
using DiscoTrack.Infrastructure.Providers;
using DiscoTrack.ListenHistory;

namespace DiscoTrack.Infrastructure.ListenHistory;

public class SpotifyListenHistorySource(ISpotifyClient client) : IListenHistorySource
{
	// The most Spotify returns, and all it remembers
	private const int MaxPlays = 50;

	public Provider TrackProvider
	{
		get { return Provider.SpotifyTrack; }
	}

	public async Task<IReadOnlyList<Play>> GetPlays(DateTimeOffset? after, CancellationToken ct)
	{
		var history = await client.GetRecentlyPlayed(MaxPlays, after?.ToUnixTimeMilliseconds(), ct);

		return history
			.Where(played => played.Track.Id is not null)
			.Select(ToPlay)
			.ToList();
	}

	private static Play ToPlay(PlayHistory played)
	{
		return new Play { PlayedAt = played.PlayedAt, Track = played.Track.ToProviderTrack() };
	}
}
