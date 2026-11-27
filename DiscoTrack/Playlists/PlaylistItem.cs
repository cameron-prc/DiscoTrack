using DiscoTrack.Providers;

namespace DiscoTrack.Playlists;

public record PlaylistItem(int Position, DateTimeOffset? AddedAt, ProviderTrack Track);
