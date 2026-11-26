using System.Net;
using System.Text;
using DiscoTrack.Infrastructure.Clients.Spotify;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tests.Clients.Spotify;

public class SpotifyAccountsClientTests
{
	private const string TokenBody = """
		{
		  "access_token": "access-1",
		  "token_type": "Bearer",
		  "scope": "user-read-recently-played",
		  "expires_in": 3600,
		  "refresh_token": "refresh-1"
		}
		""";

	private static readonly SpotifyOptions SpotifyOptions = new()
	{
		ClientId = "client-id",
		ClientSecret = "client-secret",
		RedirectUri = "https://disco.example/oauth/spotify/callback",
		AccountsBaseUrl = "https://accounts.test/",
	};

	private readonly StubHttpHandler _http = new();
	private string? _form;

	// The client disposes the request once it has the response, so the form is read while the request is live
	private void WhenTokenIsRequested(string body)
	{
		_http.When(
			request =>
			{
				_form = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
				return request.RequestUri!.AbsolutePath == "/api/token";
			},
			body);
	}

	private SpotifyAccountsClient Client()
	{
		return new(new HttpClient(_http) { BaseAddress = new Uri(SpotifyOptions.AccountsBaseUrl) }, Options.Create(SpotifyOptions));
	}

	[Fact]
	public async Task ExchangeCode_posts_the_code_and_redirect_address_with_the_client_credentials()
	{
		WhenTokenIsRequested(TokenBody);

		await Client().ExchangeCode("the-code", CancellationToken.None);

		var request = _http.Requests.Single();
		Assert.Equal(HttpMethod.Post, request.Method);
		Assert.Equal("/api/token", request.RequestUri!.AbsolutePath);
		Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
		Assert.Equal("client-id:client-secret", Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!)));
		Assert.Equal(
			"grant_type=authorization_code&code=the-code&redirect_uri=https%3A%2F%2Fdisco.example%2Foauth%2Fspotify%2Fcallback",
			_form);
	}

	[Fact]
	public async Task ExchangeCode_returns_the_tokens_as_deserialised()
	{
		_http.WhenPathContains("api/token", TokenBody);

		var response = await Client().ExchangeCode("the-code", CancellationToken.None);

		Assert.Equal("access-1", response.AccessToken);
		Assert.Equal("refresh-1", response.RefreshToken);
		Assert.Equal(3600, response.ExpiresIn);
		Assert.Equal("user-read-recently-played", response.Scope);
	}

	[Fact]
	public async Task RefreshToken_posts_the_refresh_token_and_allows_a_response_without_a_new_one()
	{
		WhenTokenIsRequested("""{ "access_token": "access-2", "token_type": "Bearer", "expires_in": 3600 }""");

		var response = await Client().RefreshToken("refresh-1", CancellationToken.None);

		Assert.Equal("grant_type=refresh_token&refresh_token=refresh-1", _form);
		Assert.Equal("access-2", response.AccessToken);
		Assert.Null(response.RefreshToken);
	}

	[Fact]
	public async Task Any_call_throws_with_the_error_description_when_the_status_is_not_successful()
	{
		_http.WhenPathContains("api/token", """{ "error": "invalid_grant", "error_description": "Invalid authorization code" }""", HttpStatusCode.BadRequest);

		var ex = await Assert.ThrowsAsync<SpotifyException>(() => Client().ExchangeCode("used-code", CancellationToken.None));

		Assert.Equal("Invalid authorization code", ex.Message);
	}

	[Fact]
	public async Task Any_call_throws_with_the_status_when_the_error_body_is_not_json()
	{
		_http.WhenPathContains("api/token", "<html>Bad Gateway</html>", HttpStatusCode.BadGateway);

		var ex = await Assert.ThrowsAsync<SpotifyException>(() => Client().RefreshToken("refresh-1", CancellationToken.None));

		Assert.Equal("Spotify returned status 502 with no message.", ex.Message);
	}
}
