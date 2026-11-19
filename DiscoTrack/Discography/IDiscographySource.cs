using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public interface IDiscographySource
{
	Task<DiscographyAlbum?> GetAlbum(MusicBrainzReleaseId releaseId, CancellationToken ct);
	Task<DiscographyAlbum?> GetAlbum(MusicBrainzReleaseGroupId releaseGroupId, CancellationToken ct);
	Task<IReadOnlyList<DiscographyAlbum>> SearchAlbums(MusicBrainzArtistId artistId, string title, CancellationToken ct);
	Task<IReadOnlyList<DiscographyArtist>> SearchArtists(string name, CancellationToken ct);
}
