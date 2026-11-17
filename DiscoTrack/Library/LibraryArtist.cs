using DiscoTrack.ExternalIds;

namespace DiscoTrack.Library;

public record LibraryArtist(ExternalId Id, string Name, MusicBrainzId? MusicBrainzId = null);
