using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public record DiscographyTrack(
	ExternalId RecordingId,
	string Title,
	int DiscNumber,
	int Position,
	int? LengthMs,
	IReadOnlyList<DiscographyArtist> Artists);
