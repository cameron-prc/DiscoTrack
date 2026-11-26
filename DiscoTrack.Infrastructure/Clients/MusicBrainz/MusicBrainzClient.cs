using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Responses;

namespace DiscoTrack.Infrastructure.Clients.MusicBrainz;

public class MusicBrainzClient(HttpClient http) : IMusicBrainzClient
{
	private const string JsonFormat = "json";

	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
	};

	public Task<Release?> GetRelease(string id, CancellationToken ct)
	{
		return Get<Release>($"{MusicBrainzRoutes.Release}/{Uri.EscapeDataString(id)}?inc=release-groups+artist-credits+recordings&fmt={JsonFormat}", ct);
	}

	public Task<ReleaseGroup?> GetReleaseGroup(string id, CancellationToken ct)
	{
		return Get<ReleaseGroup>($"{MusicBrainzRoutes.ReleaseGroup}/{Uri.EscapeDataString(id)}?inc=artist-credits&fmt={JsonFormat}", ct);
	}

	public Task<Artist?> GetArtist(string id, CancellationToken ct)
	{
		return Get<Artist>($"{MusicBrainzRoutes.Artist}/{Uri.EscapeDataString(id)}?fmt={JsonFormat}", ct);
	}

	public async Task<IReadOnlyList<Release>> BrowseReleases(string releaseGroupId, int limit, int offset, CancellationToken ct)
	{
		var query = new Dictionary<string, string>
		{
			["release-group"] = releaseGroupId,
			["inc"] = "media",
		};

		var response = await Get<ReleaseBrowseResponse>(BuildListUrl(MusicBrainzRoutes.Release, query, limit, offset), ct);

		return response?.Releases ?? [];
	}

	public async Task<IReadOnlyList<ReleaseGroup>> BrowseReleaseGroups(string artistId, IReadOnlyList<string> types, int limit, int offset, CancellationToken ct)
	{
		var query = new Dictionary<string, string>
		{
			["artist"] = artistId,
			["type"] = string.Join("|", types),
			["release-group-status"] = "website-default",
			["inc"] = "artist-credits",
		};

		var response = await Get<ReleaseGroupBrowseResponse>(BuildListUrl(MusicBrainzRoutes.ReleaseGroup, query, limit, offset), ct);

		return response?.ReleaseGroups ?? [];
	}

	public async Task<IReadOnlyList<ReleaseGroup>> SearchReleaseGroups(string query, int limit, int offset, CancellationToken ct)
	{
		var response = await Get<ReleaseGroupSearchResponse>(BuildSearchUrl(MusicBrainzRoutes.ReleaseGroup, query, limit, offset), ct);

		return response?.ReleaseGroups ?? [];
	}

	public async Task<IReadOnlyList<Artist>> SearchArtists(string query, int limit, int offset, CancellationToken ct)
	{
		var response = await Get<ArtistSearchResponse>(BuildSearchUrl(MusicBrainzRoutes.Artist, query, limit, offset), ct);

		return response?.Artists ?? [];
	}

	private async Task<T?> Get<T>(string url, CancellationToken ct) where T : class
	{
		using var response = await http.GetAsync(url, ct);

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}

		if (!response.IsSuccessStatusCode)
		{
			throw new MusicBrainzException(await ReadError(response, ct) ?? $"MusicBrainz returned status {(int)response.StatusCode} with no message.");
		}

		return await response.Content.ReadFromJsonAsync<T>(Json, ct)
			?? throw new MusicBrainzException("Empty response from MusicBrainz.");
	}

	private static async Task<string?> ReadError(HttpResponseMessage response, CancellationToken ct)
	{
		try
		{
			return (await response.Content.ReadFromJsonAsync<MusicBrainzError>(Json, ct))?.Error;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static string BuildSearchUrl(string route, string query, int limit, int offset)
	{
		return BuildListUrl(route, new Dictionary<string, string> { ["query"] = query }, limit, offset);
	}

	private static string BuildListUrl(string route, IReadOnlyDictionary<string, string> query, int limit, int offset)
	{
		var parameters = query.Concat(new Dictionary<string, string>
		{
			["limit"] = limit.ToString(),
			["offset"] = offset.ToString(),
			["fmt"] = JsonFormat,
		});

		var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
		return $"{route}?{queryString}";
	}
}
