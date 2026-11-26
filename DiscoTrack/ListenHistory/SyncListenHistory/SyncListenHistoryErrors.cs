namespace DiscoTrack.ListenHistory.SyncListenHistory;

public interface ISyncListenHistoryError;

public record UnableToReadListenHistoryError(string Message) : ISyncListenHistoryError;

public record UnableToReadListenHistorySourceError(string Message) : ISyncListenHistoryError;

public record UnableToSavePlaysError(string Message) : ISyncListenHistoryError;
