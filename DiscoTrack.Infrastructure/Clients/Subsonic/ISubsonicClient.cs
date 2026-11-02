using DiscoTrack.Infrastructure.Clients.Subsonic.Models;

namespace DiscoTrack.Infrastructure.Clients.Subsonic;

public interface ISubsonicClient
{
	Task<IReadOnlyList<AlbumID3>> GetAlbumList2(AlbumListType type, int size, int offset, CancellationToken ct);
	Task<IReadOnlyList<IndexID3>> GetArtists(CancellationToken ct);
}
