using DiscoTrack.Infrastructure.Clients.Subsonic.Models;

namespace DiscoTrack.Infrastructure.Clients.Subsonic.Responses;

internal sealed class ArtistsResponse : SubsonicResponse
{
	public Artists? Artists { get; init; }
}

internal sealed class Artists
{
	public IReadOnlyList<IndexID3> Index { get; init; } = [];
}
