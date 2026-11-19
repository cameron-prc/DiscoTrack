using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public record DiscographyAlbum(
	MusicBrainzReleaseGroupId ReleaseGroupId,
	IReadOnlyList<MusicBrainzReleaseId> ReleaseIds,
	string Title,
	int? Year,
	IReadOnlyList<DiscographyArtist> Artists);
