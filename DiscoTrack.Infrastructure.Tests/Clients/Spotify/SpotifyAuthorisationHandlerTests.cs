using DiscoTrack.Infrastructure.Clients.Spotify;
using Moq;

namespace DiscoTrack.Infrastructure.Tests.Clients.Spotify;

public class SpotifyAuthorisationHandlerTests
{
	[Fact]
	public async Task Sends_each_request_with_a_current_access_token_as_its_bearer()
	{
		var accessTokens = new Mock<ISpotifyAccessTokens>();
		accessTokens.Setup(tokens => tokens.GetAccessToken(It.IsAny<CancellationToken>())).ReturnsAsync("access-1");
		var http = new StubHttpHandler().WhenPathContains("recently-played", "{}");
		using var client = new HttpClient(new SpotifyAuthorisationHandler(accessTokens.Object) { InnerHandler = http }) { BaseAddress = new Uri("https://api.test/v1/") };

		await client.GetAsync("me/player/recently-played");

		var authorisation = http.Requests.Single().Headers.Authorization!;
		Assert.Equal("Bearer", authorisation.Scheme);
		Assert.Equal("access-1", authorisation.Parameter);
	}

	[Fact]
	public async Task Sends_nothing_when_spotify_is_not_connected()
	{
		var accessTokens = new Mock<ISpotifyAccessTokens>();
		accessTokens.Setup(tokens => tokens.GetAccessToken(It.IsAny<CancellationToken>())).ThrowsAsync(new SpotifyException("Spotify is not connected."));
		var http = new StubHttpHandler();
		using var client = new HttpClient(new SpotifyAuthorisationHandler(accessTokens.Object) { InnerHandler = http }) { BaseAddress = new Uri("https://api.test/v1/") };

		await Assert.ThrowsAsync<SpotifyException>(() => client.GetAsync("me/player/recently-played"));

		Assert.Empty(http.Requests);
	}
}
