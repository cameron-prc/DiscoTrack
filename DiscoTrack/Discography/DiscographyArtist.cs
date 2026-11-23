using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public record DiscographyArtist(ExternalId Id, string Name, string? Disambiguation);
