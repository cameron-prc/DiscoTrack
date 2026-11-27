using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Responses;

internal sealed class UrlResponse
{
	public IReadOnlyList<UrlRelation> Relations { get; init; } = [];
}

internal sealed class UrlRelation
{
	public Release? Release { get; init; }
}
