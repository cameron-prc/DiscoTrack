using DiscoTrack.Albums;

namespace DiscoTrack.ListenHistory.ResolvePlays;

public record ResolvePlaysResponse(IReadOnlyList<Play> ResolvedPlays, IReadOnlyList<Play> UnresolvedPlays, IReadOnlyList<Album> CreatedAlbums);
