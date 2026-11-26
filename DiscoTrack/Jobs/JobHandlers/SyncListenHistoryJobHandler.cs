using DiscoTrack.ListenHistory.SyncListenHistory;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs.JobHandlers;

public class SyncListenHistoryJobHandler(ISyncListenHistoryService syncListenHistory, ILogger<SyncListenHistoryJobHandler> logger) : ISyncListenHistoryJobHandler
{
	public async Task Execute(CancellationToken cancellationToken)
	{
		var result = await syncListenHistory.Run(cancellationToken);

		if (result.IsLeft)
		{
			foreach (var error in result.GetLeft)
			{
				LogError(error);
			}

			throw new JobFailedException($"Listen history sync failed with {result.GetLeft.Count} errors");
		}

		var response = result.GetRight;

		logger.LogInformation(
			"Listen history sync completed. {AddedPlays} plays added of {FetchedPlays} fetched after {After}",
			response.AddedPlays,
			response.FetchedPlays,
			response.After?.ToString("O") ?? "the beginning");
	}

	private void LogError(ISyncListenHistoryError error)
	{
		switch (error)
		{
			case UnableToReadListenHistoryError readListenHistory:
				logger.LogError("Listen history sync failed to read the plays already copied: {Message}", readListenHistory.Message);
				break;
			case UnableToReadListenHistorySourceError readSource:
				logger.LogError("Listen history sync failed to read plays from the provider: {Message}", readSource.Message);
				break;
			case UnableToSavePlaysError savePlays:
				logger.LogError("Listen history sync failed to save plays: {Message}", savePlays.Message);
				break;
			default:
				logger.LogError("Listen history sync failed: {Error}", error);
				break;
		}
	}
}
