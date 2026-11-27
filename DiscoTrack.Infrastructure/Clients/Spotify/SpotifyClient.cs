using System.Net;
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

	// What describes a playlist, without its items
	private const string PlaylistFields = "id,name,description,snapshot_id,owner(id,display_name),tracks(total)";

	public async Task<IReadOnlyList<PlayHistory>> GetRecentlyPlayed(int limit, long? afterUnixMilliseconds, CancellationToken ct)
	{
		var query = new Dictionary<string, string> { ["limit"] = limit.ToString() };
		if (afterUnixMilliseconds is { } after)
		{
			query["after"] = after.ToString();
		}

		var queryString = string.Join("&", query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

		var page = await Get<RecentlyPlayedResponse>($"{SpotifyRoutes.RecentlyPlayed}?{queryString}", ct);

		return page.Items;
	}


	public async Task<Playlist?> GetPlaylist(string playlistId, CancellationToken ct)
	{
		using var response = await http.GetAsync($"{SpotifyRoutes.Playlists}/{Uri.EscapeDataString(playlistId)}?fields={PlaylistFields}", ct);

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}

		return await Read<Playlist>(response, ct);
	}

	public Task<Page<PlaylistItem>> GetPlaylistItems(string playlistId, int limit, int offset, CancellationToken ct)
	{
		return Get<Page<PlaylistItem>>($"{SpotifyRoutes.Playlists}/{Uri.EscapeDataString(playlistId)}/tracks?limit={limit}&offset={offset}", ct);
	}

	private async Task<T> Get<T>(string path, CancellationToken ct)
	{
		using var response = await http.GetAsync(path, ct);

		return await Read<T>(response, ct);
	}

	private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
	{
		if (!response.IsSuccessStatusCode)
		{
			throw new SpotifyException(await ReadError(response, ct) ?? $"Spotify returned status {(int)response.StatusCode} with no message.");
		}

		return await response.Content.ReadFromJsonAsync<T>(Json, ct)
			?? throw new SpotifyException("Empty response from Spotify.");
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
