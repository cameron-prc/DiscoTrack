using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

namespace DiscoTrack.Infrastructure.Clients.MusicBrainz;

public interface IMusicBrainzClient
{
	Task<Release?> GetRelease(string id, CancellationToken ct);
	Task<ReleaseGroup?> GetReleaseGroup(string id, CancellationToken ct);
	Task<Artist?> GetArtist(string id, CancellationToken ct);
	Task<IReadOnlyList<Release>> BrowseReleases(string releaseGroupId, int limit, int offset, CancellationToken ct);
	Task<IReadOnlyList<ReleaseGroup>> BrowseReleaseGroups(string artistId, IReadOnlyList<string> types, int limit, int offset, CancellationToken ct);
	Task<IReadOnlyList<ReleaseGroup>> SearchReleaseGroups(string query, int limit, int offset, CancellationToken ct);
	Task<IReadOnlyList<Artist>> SearchArtists(string query, int limit, int offset, CancellationToken ct);
}
