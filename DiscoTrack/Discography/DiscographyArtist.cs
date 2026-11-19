using DiscoTrack.ExternalIds;

namespace DiscoTrack.Discography;

public record DiscographyArtist(MusicBrainzArtistId Id, string Name, string? Disambiguation);
