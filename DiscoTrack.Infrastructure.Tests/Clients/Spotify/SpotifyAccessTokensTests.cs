using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Clients.Spotify.Responses;
using DiscoTrack.Infrastructure.Tokens;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace DiscoTrack.Infrastructure.Tests.Clients.Spotify;

public class SpotifyAccessTokensTests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

	private readonly Mock<ISpotifyAccountsClient> _client = new();
	private readonly Mock<IProviderTokenStore> _store = new();
	private readonly FakeTimeProvider _time = new(Now);

	private SpotifyAccessTokens AccessTokens()
	{
		return new SpotifyAccessTokens(_client.Object, _store.Object, _time);
	}

	[Fact]
	public async Task GetAccessToken_returns_the_stored_token_while_it_has_more_than_a_minute_left()
	{
		_store.Setup(store => store.Get("Spotify", It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderToken("access-1", "refresh-1", Now.AddSeconds(61)));

		Assert.Equal("access-1", await AccessTokens().GetAccessToken(CancellationToken.None));
		_client.Verify(client => client.RefreshToken(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetAccessToken_refreshes_and_saves_a_token_about_to_expire_keeping_the_refresh_token_when_none_is_returned()
	{
		_store.Setup(store => store.Get("Spotify", It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderToken("access-1", "refresh-1", Now.AddSeconds(60)));
		_client.Setup(client => client.RefreshToken("refresh-1", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new TokenResponse { AccessToken = "access-2", ExpiresIn = 3600 });

		var accessToken = await AccessTokens().GetAccessToken(CancellationToken.None);

		Assert.Equal("access-2", accessToken);
		_store.Verify(store => store.Save("Spotify", new ProviderToken("access-2", "refresh-1", Now.AddHours(1)), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetAccessToken_saves_a_new_refresh_token_when_spotify_rotates_it()
	{
		_store.Setup(store => store.Get("Spotify", It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderToken("access-1", "refresh-1", Now.AddHours(-1)));
		_client.Setup(client => client.RefreshToken("refresh-1", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new TokenResponse { AccessToken = "access-2", RefreshToken = "refresh-2", ExpiresIn = 3600 });

		await AccessTokens().GetAccessToken(CancellationToken.None);

		_store.Verify(store => store.Save("Spotify", new ProviderToken("access-2", "refresh-2", Now.AddHours(1)), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetAccessToken_throws_when_spotify_is_not_connected()
	{
		var ex = await Assert.ThrowsAsync<SpotifyException>(() => AccessTokens().GetAccessToken(CancellationToken.None));

		Assert.Equal("Spotify is not connected.", ex.Message);
	}
}
