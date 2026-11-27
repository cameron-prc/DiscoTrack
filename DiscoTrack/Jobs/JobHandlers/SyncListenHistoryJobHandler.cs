using DiscoTrack.ListenHistory.ResolvePlays;
using DiscoTrack.ListenHistory.SyncListenHistory;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs.JobHandlers;

public class SyncListenHistoryJobHandler(ISyncListenHistoryService syncListenHistory, IResolvePlaysService resolvePlays, ILogger<SyncListenHistoryJobHandler> logger) : ISyncListenHistoryJobHandler
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
			response.AddedPlays.Count,
			response.FetchedPlays,
			response.After?.ToString("O") ?? "the beginning");

		var resolution = await resolvePlays.Run(response.AddedPlays, cancellationToken);

		if (resolution.IsLeft)
		{
			foreach (var error in resolution.GetLeft)
			{
				logger.LogError("Listen history sync failed to resolve the added plays: {Error}", error);
			}

			throw new JobFailedException($"Listen history sync copied {response.AddedPlays.Count} plays but could not resolve them");
		}

		logger.LogInformation(
			"{ResolvedPlays} added plays resolved to catalogue tracks, {UnresolvedPlays} not resolved, {CreatedAlbums} albums created",
			resolution.GetRight.ResolvedPlays.Count,
			resolution.GetRight.UnresolvedPlays.Count,
			resolution.GetRight.CreatedAlbums.Count);
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
