using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Responses;

internal sealed class ReleaseGroupSearchResponse
{
	public IReadOnlyList<ReleaseGroup> ReleaseGroups { get; init; } = [];
}
