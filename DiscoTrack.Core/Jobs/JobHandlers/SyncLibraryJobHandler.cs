using DiscoTrack.Core.Library;

namespace DiscoTrack.Core.Jobs.JobHandlers;

public class SyncLibraryJobHandler(SyncLibrary syncLibrary) : ISyncLibraryHandler
{
	public async Task Execute(CancellationToken cancellationToken)
	{
		await syncLibrary.Run(cancellationToken);
	}
}
