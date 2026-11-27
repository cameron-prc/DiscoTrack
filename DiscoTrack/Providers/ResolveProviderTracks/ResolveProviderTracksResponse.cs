using DiscoTrack.Albums;

namespace DiscoTrack.Providers.ResolveProviderTracks;

public record ResolveProviderTracksResponse(IReadOnlyList<ProviderTrack> ResolvedTracks, IReadOnlyList<ProviderTrack> UnresolvedTracks, IReadOnlyList<Album> CreatedAlbums);
