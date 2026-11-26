using System.Web;
using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Clients.Spotify.Responses;
using DiscoTrack.Infrastructure.Listening;
using DiscoTrack.Infrastructure.Tokens;
using DiscoTrack.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace DiscoTrack.Infrastructure.Tests.Listening;

public class SpotifyConnectionTests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

	private readonly Mock<ISpotifyAccountsClient> _client = new();
	private readonly Mock<IProviderTokenStore> _store = new();
	private readonly Mock<IPendingAuthorisations> _pending = new();
	private readonly FakeTimeProvider _time = new(Now);
	private readonly SpotifyOptions _options = new()
	{
		ClientId = "client-id",
		ClientSecret = "client-secret",
		RedirectUri = "https://disco.example/oauth/spotify/callback",
	};

	public SpotifyConnectionTests()
	{
		_pending.Setup(pending => pending.Start()).Returns("state-1");
		_pending.Setup(pending => pending.Complete("state-1")).Returns(true);
		_client.Setup(client => client.ExchangeCode("the-code", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new TokenResponse { AccessToken = "access-1", RefreshToken = "refresh-1", ExpiresIn = 3600 });
	}

	private SpotifyConnection Connection()
	{
		return new SpotifyConnection(_client.Object, _store.Object, _pending.Object, Options.Create(_options), _time, NullLogger<SpotifyConnection>.Instance);
	}

	[Theory]
	[InlineData("", "client-secret", "https://disco.example/callback")]
	[InlineData("client-id", "", "https://disco.example/callback")]
	[InlineData("client-id", "client-secret", "")]
	public void IsConfigured_is_false_until_the_client_and_redirect_address_are_all_set(string clientId, string clientSecret, string redirectUri)
	{
		_options.ClientId = clientId;
		_options.ClientSecret = clientSecret;
		_options.RedirectUri = redirectUri;

		Assert.False(Connection().IsConfigured);
	}

	[Fact]
	public void IsConfigured_is_true_when_the_client_and_redirect_address_are_set()
	{
		Assert.True(Connection().IsConfigured);
	}

	[Fact]
	public void StartAuthorisation_sends_the_user_to_spotify_for_recently_played_with_a_new_state()
	{
		var address = new Uri(Connection().StartAuthorisation());

		var query = HttpUtility.ParseQueryString(address.Query);
		Assert.Equal("https://accounts.spotify.com/authorize", address.GetLeftPart(UriPartial.Path));
		Assert.Equal("client-id", query["client_id"]);
		Assert.Equal("code", query["response_type"]);
		Assert.Equal("https://disco.example/oauth/spotify/callback", query["redirect_uri"]);
		Assert.Equal("user-read-recently-played", query["scope"]);
		Assert.Equal("state-1", query["state"]);
		Assert.DoesNotContain("client-secret", address.Query);
	}

	[Fact]
	public async Task CompleteAuthorisation_exchanges_the_code_and_saves_the_tokens_with_their_expiry()
	{
		var result = await Connection().CompleteAuthorisation("the-code", "state-1", null, CancellationToken.None);

		Assert.Equal(new ListeningConnected(Now.AddHours(1)), result.GetRight);
		_store.Verify(store => store.Save("Spotify", new ProviderToken("access-1", "refresh-1", Now.AddHours(1)), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("state-from-elsewhere")]
	public async Task CompleteAuthorisation_refuses_a_state_it_did_not_start_without_calling_spotify(string? state)
	{
		var result = await Connection().CompleteAuthorisation("the-code", state, null, CancellationToken.None);

		Assert.IsType<AuthorisationNotStartedHereError>(Assert.Single(result.GetLeft));
		_client.Verify(client => client.ExchangeCode(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		_store.Verify(store => store.Save(It.IsAny<string>(), It.IsAny<ProviderToken>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task CompleteAuthorisation_reports_the_reason_when_the_user_denied_access()
	{
		var result = await Connection().CompleteAuthorisation(null, "state-1", "access_denied", CancellationToken.None);

		Assert.Equal(new AuthorisationDeniedError("access_denied"), Assert.Single(result.GetLeft));
		_client.Verify(client => client.ExchangeCode(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task CompleteAuthorisation_reports_a_failed_exchange_and_saves_nothing()
	{
		_client.Setup(client => client.ExchangeCode("the-code", It.IsAny<CancellationToken>())).ThrowsAsync(new SpotifyException("Invalid authorization code"));

		var result = await Connection().CompleteAuthorisation("the-code", "state-1", null, CancellationToken.None);

		Assert.Equal(new UnableToExchangeCodeError("Invalid authorization code"), Assert.Single(result.GetLeft));
		_store.Verify(store => store.Save(It.IsAny<string>(), It.IsAny<ProviderToken>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task CompleteAuthorisation_reports_an_exchange_that_returned_no_refresh_token()
	{
		_client.Setup(client => client.ExchangeCode("the-code", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new TokenResponse { AccessToken = "access-1", ExpiresIn = 3600 });

		var result = await Connection().CompleteAuthorisation("the-code", "state-1", null, CancellationToken.None);

		Assert.IsType<UnableToExchangeCodeError>(Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task CompleteAuthorisation_reports_a_token_that_could_not_be_saved()
	{
		_store.Setup(store => store.Save("Spotify", It.IsAny<ProviderToken>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Connection().CompleteAuthorisation("the-code", "state-1", null, CancellationToken.None);

		Assert.Equal(new UnableToSaveTokenError("database is locked"), Assert.Single(result.GetLeft));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task IsConnected_follows_whether_a_token_is_stored(bool stored)
	{
		_store.Setup(store => store.Get("Spotify", It.IsAny<CancellationToken>())).ReturnsAsync(stored ? new ProviderToken("a", "r", Now) : null);

		Assert.Equal(stored, await Connection().IsConnected(CancellationToken.None));
	}

	[Fact]
	public async Task Disconnect_deletes_the_stored_token()
	{
		await Connection().Disconnect(CancellationToken.None);

		_store.Verify(store => store.Delete("Spotify", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetAccessToken_returns_the_stored_token_while_it_has_more_than_a_minute_left()
	{
		_store.Setup(store => store.Get("Spotify", It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderToken("access-1", "refresh-1", Now.AddSeconds(61)));

		Assert.Equal("access-1", await Connection().GetAccessToken(CancellationToken.None));
		_client.Verify(client => client.RefreshToken(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetAccessToken_refreshes_and_saves_a_token_about_to_expire_keeping_the_refresh_token_when_none_is_returned()
	{
		_store.Setup(store => store.Get("Spotify", It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderToken("access-1", "refresh-1", Now.AddSeconds(60)));
		_client.Setup(client => client.RefreshToken("refresh-1", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new TokenResponse { AccessToken = "access-2", ExpiresIn = 3600 });

		var accessToken = await Connection().GetAccessToken(CancellationToken.None);

		Assert.Equal("access-2", accessToken);
		_store.Verify(store => store.Save("Spotify", new ProviderToken("access-2", "refresh-1", Now.AddHours(1)), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetAccessToken_saves_a_new_refresh_token_when_spotify_rotates_it()
	{
		_store.Setup(store => store.Get("Spotify", It.IsAny<CancellationToken>())).ReturnsAsync(new ProviderToken("access-1", "refresh-1", Now.AddHours(-1)));
		_client.Setup(client => client.RefreshToken("refresh-1", It.IsAny<CancellationToken>()))
			.ReturnsAsync(new TokenResponse { AccessToken = "access-2", RefreshToken = "refresh-2", ExpiresIn = 3600 });

		await Connection().GetAccessToken(CancellationToken.None);

		_store.Verify(store => store.Save("Spotify", new ProviderToken("access-2", "refresh-2", Now.AddHours(1)), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetAccessToken_throws_when_spotify_is_not_connected()
	{
		var ex = await Assert.ThrowsAsync<SpotifyException>(() => Connection().GetAccessToken(CancellationToken.None));

		Assert.Equal("Spotify is not connected.", ex.Message);
	}
}
