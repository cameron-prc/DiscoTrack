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
