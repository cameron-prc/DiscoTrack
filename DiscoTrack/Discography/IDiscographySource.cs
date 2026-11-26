using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public interface IDiscographySource
{
	Provider ReleaseProvider { get; }
	Provider ReleaseGroupProvider { get; }
	Provider ArtistProvider { get; }
	Provider RecordingProvider { get; }
	ExternalId VariousArtistsId { get; }

	Task<DiscographyAlbum?> GetAlbum(ExternalId id, CancellationToken ct);
	Task<IReadOnlyList<DiscographyTrack>> GetAlbumTracks(ExternalId id, CancellationToken ct);
	Task<DiscographyArtist?> GetArtist(ExternalId id, CancellationToken ct);
	Task<IReadOnlyList<DiscographyAlbum>> SearchAlbums(ExternalId artistId, string title, CancellationToken ct);
	Task<IReadOnlyList<DiscographyAlbum>> GetArtistAlbums(ExternalId artistId, CancellationToken ct);
	Task<IReadOnlyList<DiscographyArtist>> SearchArtists(string name, CancellationToken ct);
}
