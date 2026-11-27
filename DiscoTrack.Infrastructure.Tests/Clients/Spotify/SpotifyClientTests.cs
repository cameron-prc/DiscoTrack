using System.Net;
using System.Web;
using DiscoTrack.Infrastructure.Clients.Spotify;

namespace DiscoTrack.Infrastructure.Tests.Clients.Spotify;

public class SpotifyClientTests
{
	private readonly StubHttpHandler _http = new();

	private SpotifyClient Client()
	{
		return new(new HttpClient(_http) { BaseAddress = new Uri("https://api.test/v1/") });
	}

	[Fact]
	public async Task GetRecentlyPlayed_requests_the_given_limit_and_only_plays_after_the_given_time()
	{
		_http.WhenPathContains("recently-played", Fixtures.Read("spotify/recently-played.json"));

		await Client().GetRecentlyPlayed(50, 1795731245123, CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		var query = HttpUtility.ParseQueryString(request.Query);
		Assert.Equal("/v1/me/player/recently-played", request.AbsolutePath);
		Assert.Equal("50", query["limit"]);
		Assert.Equal("1795731245123", query["after"]);
	}



	[Fact]
	public async Task GetPlaylist_requests_only_what_describes_the_playlist()
	{
		_http.WhenPathContains("playlists/", Fixtures.Read("spotify/playlist.json"));

		await Client().GetPlaylist("3cEYpjA9oz9GiPac4AsH4n", CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		Assert.Equal("/v1/playlists/3cEYpjA9oz9GiPac4AsH4n", request.AbsolutePath);
		Assert.Equal("id,name,description,snapshot_id,owner(id,display_name),tracks(total)", HttpUtility.ParseQueryString(request.Query)["fields"]);
	}

	[Fact]
	public async Task GetPlaylist_returns_the_playlist_as_deserialised()
	{
		_http.WhenPathContains("playlists/", Fixtures.Read("spotify/playlist.json"));

		var playlist = await Client().GetPlaylist("3cEYpjA9oz9GiPac4AsH4n", CancellationToken.None);

		Assert.NotNull(playlist);
		Assert.Equal(("3cEYpjA9oz9GiPac4AsH4n", "Road Trip", "For the drive north", "AAAAEnSnapshotOne"), (playlist.Id, playlist.Name, playlist.Description, playlist.SnapshotId));
		Assert.Equal("Example Listener", playlist.Owner!.DisplayName);
		Assert.Equal(42, playlist.Tracks!.Total);
	}

	[Fact]
	public async Task GetPlaylist_returns_null_when_spotify_has_no_such_playlist()
	{
		_http.WhenPathContains("playlists/", """{"error":{"status":404,"message":"Resource not found"}}""", HttpStatusCode.NotFound);

		Assert.Null(await Client().GetPlaylist("3cEYpjA9oz9GiPac4AsH4n", CancellationToken.None));
	}

	[Fact]
	public async Task GetPlaylist_throws_with_spotifys_message_for_any_other_failure()
	{
		_http.WhenPathContains("playlists/", """{"error":{"status":401,"message":"The access token expired"}}""", HttpStatusCode.Unauthorized);

		var exception = await Assert.ThrowsAsync<SpotifyException>(() => Client().GetPlaylist("3cEYpjA9oz9GiPac4AsH4n", CancellationToken.None));

		Assert.Equal("The access token expired", exception.Message);
	}

	[Fact]
	public async Task GetPlaylistItems_requests_the_given_page_of_the_playlists_tracks()
	{
		_http.WhenPathContains("/tracks", Fixtures.Read("spotify/playlist-items.json"));

		await Client().GetPlaylistItems("3cEYpjA9oz9GiPac4AsH4n", 100, 200, CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		Assert.Equal("/v1/playlists/3cEYpjA9oz9GiPac4AsH4n/tracks", request.AbsolutePath);
		Assert.Equal("?limit=100&offset=200", request.Query);
	}

	[Fact]
	public async Task GetPlaylistItems_returns_the_page_as_deserialised()
	{
		_http.WhenPathContains("/tracks", Fixtures.Read("spotify/playlist-items.json"));

		var page = await Client().GetPlaylistItems("3cEYpjA9oz9GiPac4AsH4n", 100, 0, CancellationToken.None);

		Assert.Null(page.Next);
		Assert.Equal(4, page.Items.Count);
		var song = page.Items[0]!;
		Assert.Equal(new DateTimeOffset(2026, 7, 6, 7, 10, 5, TimeSpan.Zero), song.AddedAt);
		Assert.False(song.IsLocal);
		Assert.Equal("track", song.Track!.Type);
		Assert.Equal("6tC2iHfUlzB2W4ntXXL2BH", song.Track.Id);
		Assert.Equal("GBAHT1000190", song.Track.ExternalIds!.Isrc);
		Assert.Equal((1, 12), (song.Track.DiscNumber, song.Track.TrackNumber));
		Assert.Equal("Immersion", song.Track.Album!.Name);
		Assert.Equal("episode", page.Items[1]!.Track!.Type);
		Assert.True(page.Items[2]!.IsLocal);
		Assert.Null(page.Items[2]!.Track!.Id);
		Assert.Null(page.Items[3]!.Track);
	}


	[Fact]
	public async Task GetRecentlyPlayed_sends_no_after_when_none_is_given()
	{
		_http.WhenPathContains("recently-played", Fixtures.Read("spotify/recently-played.json"));

		await Client().GetRecentlyPlayed(50, null, CancellationToken.None);

		Assert.Equal("?limit=50", _http.Requests.Single().RequestUri!.Query);
	}

	[Fact]
	public async Task GetRecentlyPlayed_returns_the_plays_as_deserialised()
	{
		_http.WhenPathContains("recently-played", Fixtures.Read("spotify/recently-played.json"));

		var plays = await Client().GetRecentlyPlayed(50, null, CancellationToken.None);

		Assert.Equal(2, plays.Count);
		var played = plays[0];
		Assert.Equal(new DateTimeOffset(2026, 11, 26, 22, 14, 5, 123, TimeSpan.Zero), played.PlayedAt);
		Assert.Equal("6tC2iHfUlzB2W4ntXXL2BH", played.Track.Id);
		Assert.Equal("Self vs Self", played.Track.Name);
		Assert.Equal(285000, played.Track.DurationMs);
		Assert.Equal((1, 12), (played.Track.DiscNumber, played.Track.TrackNumber));
		Assert.Equal("GBAHT1000190", played.Track.ExternalIds!.Isrc);
		Assert.Equal(["Pendulum", "In Flames"], played.Track.Artists!.Select(artist => artist.Name));
		Assert.Equal("4LqcOTZN8DXzvzizsRjv9v", played.Track.Album!.Id);
		Assert.Equal("Immersion", played.Track.Album.Name);
		Assert.Null(plays[1].Track.Id);
		Assert.Null(plays[1].Track.ExternalIds!.Isrc);
	}

	[Fact]
	public async Task GetRecentlyPlayed_returns_empty_when_nothing_was_played()
	{
		_http.WhenPathContains("recently-played", """{ "items": [], "limit": 50 }""");

		Assert.Empty(await Client().GetRecentlyPlayed(50, null, CancellationToken.None));
	}

	[Fact]
	public async Task Any_call_throws_with_the_server_message_when_the_status_is_not_successful()
	{
		_http.WhenPathContains("recently-played", """{ "error": { "status": 403, "message": "Insufficient client scope" } }""", HttpStatusCode.Forbidden);

		var ex = await Assert.ThrowsAsync<SpotifyException>(() => Client().GetRecentlyPlayed(50, null, CancellationToken.None));

		Assert.Equal("Insufficient client scope", ex.Message);
	}

	[Fact]
	public async Task Any_call_throws_with_the_status_when_the_error_body_is_not_json()
	{
		_http.WhenPathContains("recently-played", "<html>Bad Gateway</html>", HttpStatusCode.BadGateway);

		var ex = await Assert.ThrowsAsync<SpotifyException>(() => Client().GetRecentlyPlayed(50, null, CancellationToken.None));

		Assert.Equal("Spotify returned status 502 with no message.", ex.Message);
	}
}
