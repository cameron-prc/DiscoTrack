using System.Net;
using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using Moq;

namespace DiscoTrack.Infrastructure.Tests.Clients.MusicBrainz;

public class MusicBrainzRateLimitHandlerTests
{
	private readonly StubHttpHandler _http = new();
	private readonly Mock<IMusicBrainzRateLimiter> _rateLimiter = new();
	private int _busyResponses;

	private HttpClient Client(int busyRetries = 3)
	{
		var handler = new MusicBrainzRateLimitHandler(_rateLimiter.Object, TestMusicBrainzOptions.Create(busyRetries: busyRetries), TimeProvider.System)
		{
			InnerHandler = _http,
		};

		return new HttpClient(handler) { BaseAddress = new Uri(TestMusicBrainzOptions.BaseUrl) };
	}

	private void SetupBusy(int times)
	{
		_busyResponses = times;
		_http.When(_ => _busyResponses-- > 0, Fixtures.Read("musicbrainz/error-busy.json"), HttpStatusCode.ServiceUnavailable);
		_http.WhenPathContains("artist/", Fixtures.Read("musicbrainz/artist.json"));
	}

	[Fact]
	public async Task Send_waits_for_the_rate_limiter_before_the_request()
	{
		SetupBusy(0);

		var response = await Client().GetAsync("artist/mb-tool");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Single(_http.Requests);
		_rateLimiter.Verify(rateLimiter => rateLimiter.Wait(It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Send_sends_again_at_the_allowed_rate_while_the_server_is_busy()
	{
		SetupBusy(2);

		var response = await Client().GetAsync("artist/mb-tool");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(3, _http.Requests.Count);
		_rateLimiter.Verify(rateLimiter => rateLimiter.Wait(It.IsAny<CancellationToken>()), Times.Exactly(3));
	}

	[Fact]
	public async Task Send_returns_the_busy_response_once_the_retries_are_used_up()
	{
		SetupBusy(10);

		var response = await Client(busyRetries: 2).GetAsync("artist/mb-tool");

		Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
		Assert.Equal(3, _http.Requests.Count);
	}

	[Fact]
	public async Task Send_does_not_send_again_for_other_failures()
	{
		_http.WhenPathContains("artist/", Fixtures.Read("musicbrainz/error-not-found.json"), HttpStatusCode.NotFound);

		var response = await Client().GetAsync("artist/mb-tool");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		Assert.Single(_http.Requests);
	}
}
