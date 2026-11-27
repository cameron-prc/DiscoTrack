namespace DiscoTrack.ListenHistory.SyncListenHistory;

public record SyncListenHistoryResponse(int FetchedPlays, IReadOnlyList<Play> AddedPlays, DateTimeOffset? After);
