using DiscoTrack.Infrastructure.Clients.Subsonic.Models;

namespace DiscoTrack.Infrastructure.Clients.Subsonic.Responses;

internal sealed class Search3Response : SubsonicResponse
{
	public SearchResult3? SearchResult3 { get; init; }
}

internal sealed class SearchResult3
{
	public IReadOnlyList<Child> Song { get; init; } = [];
}
