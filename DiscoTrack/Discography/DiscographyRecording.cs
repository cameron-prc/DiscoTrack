using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public record DiscographyRecording(ExternalId RecordingId, IReadOnlyList<DiscographyRelease> Releases);

public record DiscographyRelease(ExternalId ReleaseId, string Title, bool IsOfficial, string? Date, int TrackCount);
