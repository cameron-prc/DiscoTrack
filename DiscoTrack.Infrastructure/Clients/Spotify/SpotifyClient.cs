using System.Net.Http.Json;
using System.Text.Json;
using DiscoTrack.Infrastructure.Clients.Spotify.Models;
using DiscoTrack.Infrastructure.Clients.Spotify.Responses;

namespace DiscoTrack.Infrastructure.Clients.Spotify;

public class SpotifyClient(HttpClient http) : ISpotifyClient
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
	};

	public async Task<IReadOnlyList<PlayHistory>> GetRecentlyPlayed(int limit, long? afterUnixMilliseconds, CancellationToken ct)
	{
		var query = new Dictionary<string, string> { ["limit"] = limit.ToString() };
		if (afterUnixMilliseconds is { } after)
		{
			query["after"] = after.ToString();
		}

		var queryString = string.Join("&", query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

		using var response = await http.GetAsync($"{SpotifyRoutes.RecentlyPlayed}?{queryString}", ct);

		if (!response.IsSuccessStatusCode)
		{
			throw new SpotifyException(await ReadError(response, ct) ?? $"Spotify returned status {(int)response.StatusCode} with no message.");
		}

		var page = await response.Content.ReadFromJsonAsync<RecentlyPlayedResponse>(Json, ct)
			?? throw new SpotifyException("Empty response from Spotify.");

		return page.Items;
	}

	private static async Task<string?> ReadError(HttpResponseMessage response, CancellationToken ct)
	{
		try
		{
			return (await response.Content.ReadFromJsonAsync<ApiError>(Json, ct))?.Error?.Message;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
