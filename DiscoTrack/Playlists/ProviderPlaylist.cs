using DiscoTrack.ExternalIds;

namespace DiscoTrack.Playlists;

public record ProviderPlaylist(ExternalId Id, string Name, string? Description, string? OwnerName, string? SnapshotId, int ItemCount);
