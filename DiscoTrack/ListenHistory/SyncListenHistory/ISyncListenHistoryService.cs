using DiscoTrack.Common;

namespace DiscoTrack.ListenHistory.SyncListenHistory;

public interface ISyncListenHistoryService
{
	Task<Either<IList<ISyncListenHistoryError>, SyncListenHistoryResponse>> Run(CancellationToken cancellationToken);
}
