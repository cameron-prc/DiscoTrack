using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Clients.Spotify.Models;
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
		var track = played.Track;

		return new Play
		{
			PlayedAt = played.PlayedAt,
			TrackId = new SpotifyTrackId(track.Id!),
			TrackTitle = track.Name,
			Isrc = string.IsNullOrEmpty(track.ExternalIds?.Isrc) ? null : track.ExternalIds.Isrc,
			DurationMs = track.DurationMs,
			DiscNumber = track.DiscNumber,
			TrackNumber = track.TrackNumber,
			Artists = (track.Artists ?? [])
				.Where(artist => artist.Id is not null)
				.Select(artist => new PlayArtist(new SpotifyArtistId(artist.Id!), artist.Name))
				.ToList(),
			AlbumId = track.Album?.Id is { } albumId ? new SpotifyAlbumId(albumId) : null,
			AlbumTitle = track.Album?.Name,
		};
	}
}
