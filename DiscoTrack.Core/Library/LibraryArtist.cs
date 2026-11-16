using DiscoTrack.Core.ExternalIds;

namespace DiscoTrack.Core.Library;

public record LibraryArtist(ExternalId Id, string Name, MusicBrainzId? MusicBrainzId = null);
