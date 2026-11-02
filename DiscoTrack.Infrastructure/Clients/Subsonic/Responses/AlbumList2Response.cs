using DiscoTrack.Infrastructure.Clients.Subsonic.Models;

namespace DiscoTrack.Infrastructure.Clients.Subsonic.Responses;

internal sealed class AlbumList2Response : SubsonicResponse
{
	public AlbumList2? AlbumList2 { get; init; }
}

internal sealed class AlbumList2
{
	public IReadOnlyList<AlbumID3> Album { get; init; } = [];
}
