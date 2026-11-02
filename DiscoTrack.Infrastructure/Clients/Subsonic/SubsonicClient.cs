using System.Net.Http.Json;
using System.Text.Json;
using DiscoTrack.Infrastructure.Clients.Subsonic.Models;
using DiscoTrack.Infrastructure.Clients.Subsonic.Responses;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Clients.Subsonic;

public class SubsonicClient(HttpClient http, IOptions<SubsonicOptions> options) : ISubsonicClient
{
	private const string RestPrefix = "rest";
	private const string JsonFormat = "json";
	private const string StatusOk = "ok";

	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	private readonly SubsonicOptions _options = options.Value;

	public async Task<IReadOnlyList<AlbumID3>> GetAlbumList2(AlbumListType type, int size, int offset, CancellationToken ct)
	{
		var query = new Dictionary<string, string>
		{
			["type"] = type.ToQueryValue(),
			["size"] = size.ToString(),
			["offset"] = offset.ToString(),
		};

		var response = await Get<AlbumList2Response>(SubsonicRoutes.GetAlbumList2, query, ct);

		return response.AlbumList2?.Album ?? [];
	}

	public async Task<IReadOnlyList<IndexID3>> GetArtists(CancellationToken ct)
	{
		var response = await Get<ArtistsResponse>(SubsonicRoutes.GetArtists, new Dictionary<string, string>(), ct);

		return response.Artists?.Index ?? [];
	}

	private async Task<T> Get<T>(string method, IReadOnlyDictionary<string, string> query, CancellationToken ct) where T : SubsonicResponse
	{
		var envelope = await http.GetFromJsonAsync<SubsonicEnvelope<T>>(BuildUrl(method, query), Json, ct);
		if (envelope is null)
		{
			throw new SubsonicException("Empty response from Subsonic.");
		}

		var response = envelope.Response;
		if (response.Status != StatusOk)
		{
			throw new SubsonicException(response.Error?.Message ?? $"Subsonic returned status '{response.Status}' with no message.");
		}

		return response;
	}

	private string BuildUrl(string method, IReadOnlyDictionary<string, string> query)
	{
		var parameters = query.Concat(new Dictionary<string, string>
		{
			["u"] = _options.Username,
			["t"] = _options.Token,
			["s"] = _options.Salt,
			["v"] = _options.ApiVersion,
			["c"] = _options.ClientName,
			["f"] = JsonFormat,
		});

		var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
		return $"{RestPrefix}/{method}?{queryString}";
	}
}
