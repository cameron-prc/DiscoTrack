using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.ListenHistory;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Sqlite.Migrations;
using DiscoTrack.Infrastructure.Tests.Sqlite;
using DiscoTrack.ListenHistory;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tests.ListenHistory;

public sealed class SqliteListenHistoryTests : IDisposable
{
	private static readonly DateTimeOffset Morning = new(2026, 11, 27, 8, 0, 0, 123, TimeSpan.Zero);

	private readonly TestDatabase _db = new();
	private readonly SqliteListenHistory _history;

	public SqliteListenHistoryTests()
	{
		Migrator.Migrate(_db.ConnectionString);
		_history = new SqliteListenHistory(Options.Create(new SqliteOptions { ConnectionString = _db.ConnectionString }));
	}

	public void Dispose()
	{
		_db.Dispose();
	}

	[Fact]
	public async Task A_fresh_history_is_empty()
	{
		Assert.Equal(0, await _history.CountPlays(CancellationToken.None));
		Assert.Empty(await _history.GetPlays(0, 50, CancellationToken.None));
		Assert.Null(await _history.GetLatestPlayedAt(Provider.SpotifyTrack, CancellationToken.None));
	}

	[Fact]
	public async Task AddPlays_round_trips_a_play_with_its_artists_in_order()
	{
		var play = new Play
		{
			PlayedAt = Morning,
			TrackId = new SpotifyTrackId("trk-genesis"),
			TrackTitle = "Genesis",
			Isrc = "GBAHT1000182",
			DurationMs = 69000,
			DiscNumber = 2,
			TrackNumber = 12,
			Artists = [new PlayArtist(new SpotifyArtistId("art-pendulum"), "Pendulum"), new PlayArtist(new SpotifyArtistId("art-guest"), "Guest")],
			AlbumId = new SpotifyAlbumId("alb-immersion"),
			AlbumTitle = "Immersion",
		};

		var added = await _history.AddPlays([play], CancellationToken.None);

		Assert.Same(play, Assert.Single(added));
		var stored = Assert.Single(await _history.GetPlays(0, 50, CancellationToken.None));
		Assert.Equal(play.Id, stored.Id);
		Assert.Equal(Morning, stored.PlayedAt);
		Assert.Equal(new SpotifyTrackId("trk-genesis"), stored.TrackId);
		Assert.Equal("Genesis", stored.TrackTitle);
		Assert.Equal("GBAHT1000182", stored.Isrc);
		Assert.Equal(69000, stored.DurationMs);
		Assert.Equal((2, 12), (stored.DiscNumber, stored.TrackNumber));
		Assert.Equal(play.Artists, stored.Artists);
		Assert.Equal(new SpotifyAlbumId("alb-immersion"), stored.AlbumId);
		Assert.Equal("Immersion", stored.AlbumTitle);
	}

	[Fact]
	public async Task AddPlays_round_trips_a_play_with_no_album_isrc_duration_or_artists()
	{
		await _history.AddPlays([Play("trk-bare", Morning)], CancellationToken.None);

		var stored = Assert.Single(await _history.GetPlays(0, 50, CancellationToken.None));
		Assert.Null(stored.AlbumId);
		Assert.Null(stored.AlbumTitle);
		Assert.Null(stored.Isrc);
		Assert.Null(stored.DurationMs);
		Assert.Null(stored.DiscNumber);
		Assert.Null(stored.TrackNumber);
		Assert.Empty(stored.Artists);
	}

	[Fact]
	public async Task AddPlays_adds_nothing_when_the_same_plays_are_added_again()
	{
		await _history.AddPlays([Play("trk-1", Morning), Play("trk-2", Morning.AddMinutes(4))], CancellationToken.None);

		var added = await _history.AddPlays([Play("trk-1", Morning), Play("trk-2", Morning.AddMinutes(4))], CancellationToken.None);

		Assert.Empty(added);
		Assert.Equal(2, await _history.CountPlays(CancellationToken.None));
		Assert.All(await _history.GetPlays(0, 50, CancellationToken.None), play => Assert.Single(play.Artists));
	}

	[Fact]
	public async Task AddPlays_adds_only_the_new_plays_of_an_overlapping_batch()
	{
		await _history.AddPlays([Play("trk-1", Morning)], CancellationToken.None);

		var added = await _history.AddPlays([Play("trk-1", Morning), Play("trk-2", Morning.AddMinutes(4))], CancellationToken.None);

		Assert.Equal("trk-2", Assert.Single(added).TrackTitle);
		Assert.Equal(2, await _history.CountPlays(CancellationToken.None));
	}

	[Fact]
	public async Task AddPlays_treats_the_same_instant_in_another_time_zone_as_the_same_play()
	{
		await _history.AddPlays([Play("trk-1", Morning)], CancellationToken.None);

		var added = await _history.AddPlays([Play("trk-1", Morning.ToOffset(TimeSpan.FromHours(12)))], CancellationToken.None);

		Assert.Empty(added);
	}

	[Fact]
	public async Task AddPlays_keeps_the_same_track_played_at_two_times_and_two_tracks_played_at_one_time()
	{
		var added = await _history.AddPlays(
			[Play("trk-1", Morning), Play("trk-1", Morning.AddMinutes(4)), Play("trk-2", Morning)],
			CancellationToken.None);

		Assert.Equal(3, added.Count);
	}

	[Fact]
	public async Task GetPlays_pages_from_the_most_recent_play_back()
	{
		await _history.AddPlays(
			[Play("trk-old", Morning), Play("trk-new", Morning.AddHours(2)), Play("trk-mid", Morning.AddHours(1))],
			CancellationToken.None);

		var firstPage = await _history.GetPlays(0, 2, CancellationToken.None);
		var secondPage = await _history.GetPlays(2, 2, CancellationToken.None);

		Assert.Equal(["trk-new", "trk-mid"], firstPage.Select(play => play.TrackTitle));
		Assert.Equal(["trk-old"], secondPage.Select(play => play.TrackTitle));
	}

	[Fact]
	public async Task GetLatestPlayedAt_returns_the_most_recent_play_of_the_provider_to_the_millisecond()
	{
		await _history.AddPlays([Play("trk-1", Morning), Play("trk-2", Morning.AddMinutes(4).AddMilliseconds(7))], CancellationToken.None);

		Assert.Equal(Morning.AddMinutes(4).AddMilliseconds(7), await _history.GetLatestPlayedAt(Provider.SpotifyTrack, CancellationToken.None));
		Assert.Null(await _history.GetLatestPlayedAt(Provider.Subsonic, CancellationToken.None));
	}

	private static Play Play(string trackId, DateTimeOffset playedAt)
	{
		return trackId == "trk-bare"
			? new Play { PlayedAt = playedAt, TrackId = new SpotifyTrackId(trackId), TrackTitle = trackId }
			: new Play { PlayedAt = playedAt, TrackId = new SpotifyTrackId(trackId), TrackTitle = trackId, Artists = [new PlayArtist(new SpotifyArtistId("art-1"), "Artist")] };
	}
}
