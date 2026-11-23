using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public record DiscographyAlbum(
	ExternalId ReleaseGroupId,
	IReadOnlyList<ExternalId> ReleaseIds,
	string Title,
	int? Year,
	bool IsCompilation,
	IReadOnlyList<DiscographyArtist> Artists);
