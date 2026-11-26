namespace DiscoTrack.ListenHistory.SyncListenHistory;

public record SyncListenHistoryResponse(int FetchedPlays, int AddedPlays, DateTimeOffset? After);
