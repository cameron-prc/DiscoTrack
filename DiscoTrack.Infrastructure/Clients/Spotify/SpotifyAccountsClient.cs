using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DiscoTrack.Infrastructure.Clients.Spotify.Responses;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Clients.Spotify;

public class SpotifyAccountsClient(HttpClient http, IOptions<SpotifyOptions> options) : ISpotifyAccountsClient
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
	};

	private readonly SpotifyOptions _options = options.Value;

	public Task<TokenResponse> ExchangeCode(string code, CancellationToken ct)
	{
		return PostToken(new Dictionary<string, string>
		{
			["grant_type"] = "authorization_code",
			["code"] = code,
			["redirect_uri"] = _options.RedirectUri,
		}, ct);
	}

	public Task<TokenResponse> RefreshToken(string refreshToken, CancellationToken ct)
	{
		return PostToken(new Dictionary<string, string>
		{
			["grant_type"] = "refresh_token",
			["refresh_token"] = refreshToken,
		}, ct);
	}

	private async Task<TokenResponse> PostToken(IReadOnlyDictionary<string, string> form, CancellationToken ct)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, SpotifyRoutes.Token);
		request.Headers.Authorization = new AuthenticationHeaderValue(
			"Basic",
			Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")));
		request.Content = new FormUrlEncodedContent(form);

		using var response = await http.SendAsync(request, ct);

		if (!response.IsSuccessStatusCode)
		{
			throw new SpotifyException(await ReadError(response, ct) ?? $"Spotify returned status {(int)response.StatusCode} with no message.");
		}

		return await response.Content.ReadFromJsonAsync<TokenResponse>(Json, ct)
			?? throw new SpotifyException("Empty response from Spotify.");
	}

	private static async Task<string?> ReadError(HttpResponseMessage response, CancellationToken ct)
	{
		try
		{
			var error = await response.Content.ReadFromJsonAsync<TokenError>(Json, ct);

			return error?.ErrorDescription ?? error?.Error;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
