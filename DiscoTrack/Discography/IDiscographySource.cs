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

	/// <summary>The releases the discography links to another provider's album. Empty when it knows of none.</summary>
	Task<IReadOnlyList<ExternalId>> FindLinkedReleases(ExternalId albumId, CancellationToken ct);

	/// <summary>The recordings that carry an ISRC, each with the releases it appears on.</summary>
	Task<IReadOnlyList<DiscographyRecording>> FindRecordings(string isrc, CancellationToken ct);
	Task<DiscographyArtist?> GetArtist(ExternalId id, CancellationToken ct);
	Task<IReadOnlyList<DiscographyAlbum>> SearchAlbums(ExternalId artistId, string title, CancellationToken ct);
	Task<IReadOnlyList<DiscographyAlbum>> GetArtistAlbums(ExternalId artistId, CancellationToken ct);
	Task<IReadOnlyList<DiscographyArtist>> SearchArtists(string name, CancellationToken ct);
}
