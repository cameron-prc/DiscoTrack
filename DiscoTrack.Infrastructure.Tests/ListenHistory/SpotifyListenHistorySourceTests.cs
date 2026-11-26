using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Clients.Spotify.Models;
using DiscoTrack.Infrastructure.ListenHistory;
using DiscoTrack.ListenHistory;
using Moq;

namespace DiscoTrack.Infrastructure.Tests.ListenHistory;

public class SpotifyListenHistorySourceTests
{
	private static readonly DateTimeOffset PlayedAt = new(2026, 11, 26, 22, 14, 5, 123, TimeSpan.Zero);

	private readonly Mock<ISpotifyClient> _client = new();

	private SpotifyListenHistorySource Source()
	{
		return new SpotifyListenHistorySource(_client.Object);
	}

	private void SetupPlays(params PlayHistory[] plays)
	{
		_client.Setup(client => client.GetRecentlyPlayed(It.IsAny<int>(), It.IsAny<long?>(), It.IsAny<CancellationToken>())).ReturnsAsync(plays);
	}

	[Fact]
	public void TrackProvider_is_the_spotify_track_provider()
	{
		Assert.Equal(Provider.SpotifyTrack, Source().TrackProvider);
	}

	[Fact]
	public async Task GetPlays_asks_for_the_fifty_spotify_keeps_after_the_given_time_in_milliseconds()
	{
		SetupPlays();

		await Source().GetPlays(PlayedAt, CancellationToken.None);

		_client.Verify(client => client.GetRecentlyPlayed(50, PlayedAt.ToUnixTimeMilliseconds(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetPlays_asks_without_a_time_when_none_is_given()
	{
		SetupPlays();

		await Source().GetPlays(null, CancellationToken.None);

		_client.Verify(client => client.GetRecentlyPlayed(50, null, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetPlays_maps_the_track_its_artists_in_order_and_its_album()
	{
		SetupPlays(new PlayHistory
		{
			PlayedAt = PlayedAt,
			Track = new Track
			{
				Id = "trk-self", Name = "Self vs Self", DurationMs = 285000,
				ExternalIds = new TrackExternalIds { Isrc = "GBAHT1000190" },
				Artists = [new Artist { Id = "art-pendulum", Name = "Pendulum" }, new Artist { Id = "art-in-flames", Name = "In Flames" }],
				Album = new Album { Id = "alb-immersion", Name = "Immersion" },
			},
		});

		var play = Assert.Single(await Source().GetPlays(null, CancellationToken.None));

		Assert.Equal(PlayedAt, play.PlayedAt);
		Assert.Equal(new SpotifyTrackId("trk-self"), play.TrackId);
		Assert.Equal("Self vs Self", play.TrackTitle);
		Assert.Equal("GBAHT1000190", play.Isrc);
		Assert.Equal(285000, play.DurationMs);
		Assert.Equal([new PlayArtist(new SpotifyArtistId("art-pendulum"), "Pendulum"), new PlayArtist(new SpotifyArtistId("art-in-flames"), "In Flames")], play.Artists);
		Assert.Equal(new SpotifyAlbumId("alb-immersion"), play.AlbumId);
		Assert.Equal("Immersion", play.AlbumTitle);
	}

	[Fact]
	public async Task GetPlays_skips_a_local_file_that_has_no_spotify_track_id()
	{
		SetupPlays(
			new PlayHistory { PlayedAt = PlayedAt, Track = new Track { Id = null, Name = "Demo" } },
			new PlayHistory { PlayedAt = PlayedAt.AddMinutes(-5), Track = new Track { Id = "trk-1", Name = "Genesis" } });

		var play = Assert.Single(await Source().GetPlays(null, CancellationToken.None));

		Assert.Equal("Genesis", play.TrackTitle);
	}

	[Fact]
	public async Task GetPlays_maps_a_track_without_isrc_album_id_or_artist_ids()
	{
		SetupPlays(new PlayHistory
		{
			PlayedAt = PlayedAt,
			Track = new Track
			{
				Id = "trk-1", Name = "Genesis",
				ExternalIds = new TrackExternalIds { Isrc = "" },
				Artists = [new Artist { Id = null, Name = "Unknown" }],
				Album = new Album { Id = null, Name = "Bootleg" },
			},
		});

		var play = Assert.Single(await Source().GetPlays(null, CancellationToken.None));

		Assert.Null(play.Isrc);
		Assert.Empty(play.Artists);
		Assert.Null(play.AlbumId);
		Assert.Equal("Bootleg", play.AlbumTitle);
	}
}
