using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public record DiscographyAlbum(
	ExternalId ReleaseGroupId,
	string Title,
	int? Year,
	bool IsCompilation,
	IReadOnlyList<DiscographyArtist> Artists,
	IReadOnlyList<DiscographyTrack>? Tracks = null);
