using DiscoTrack.Common;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.ListenHistory.SyncListenHistory;

public class SyncListenHistoryService(IListenHistorySource source, IListenHistory listenHistory, ILogger<SyncListenHistoryService> logger) : ISyncListenHistoryService
{
	/// <summary>
	/// <para>Copies the plays the source holds that the listen history does not.</para>
	/// <para>Only plays after the latest one already copied are asked for, and a play already held is never added again, so running this twice adds nothing the second time.</para>
	/// </summary>
	public async Task<Either<IList<ISyncListenHistoryError>, SyncListenHistoryResponse>> Run(CancellationToken cancellationToken)
	{
		DateTimeOffset? latestPlayedAt;
		IReadOnlyList<Play> plays;
		IReadOnlyList<Play> addedPlays;

		try
		{
			latestPlayedAt = await listenHistory.GetLatestPlayedAt(source.TrackProvider, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncListenHistoryError> { new UnableToReadListenHistoryError(exception.Message) };
		}

		try
		{
			plays = await source.GetPlays(latestPlayedAt, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncListenHistoryError> { new UnableToReadListenHistorySourceError(exception.Message) };
		}

		try
		{
			addedPlays = await listenHistory.AddPlays(plays, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save {PlayCount} plays", plays.Count);

			return new List<ISyncListenHistoryError> { new UnableToSavePlaysError(exception.Message) };
		}

		return new SyncListenHistoryResponse(plays.Count, addedPlays, latestPlayedAt);
	}
}
