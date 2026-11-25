using DiscoTrack.Jobs.JobHandlers;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs;

public class JobRunner : IJobRunner
{
	private readonly ISyncLibraryHandler _syncLibraryHandler;
	private readonly ISyncAlbumWithDiscographyJobHandler _syncAlbumWithDiscographyJobHandler;
	private readonly ISyncArtistWithDiscographyJobHandler _syncArtistWithDiscographyJobHandler;
	private readonly ILogger<JobRunner> _logger;

	public JobRunner(ISyncLibraryHandler syncLibraryHandler, ISyncAlbumWithDiscographyJobHandler syncAlbumWithDiscographyJobHandler, ISyncArtistWithDiscographyJobHandler syncArtistWithDiscographyJobHandler, ILogger<JobRunner> logger)
	{
		_syncLibraryHandler = syncLibraryHandler;
		_syncAlbumWithDiscographyJobHandler = syncAlbumWithDiscographyJobHandler;
		_syncArtistWithDiscographyJobHandler = syncArtistWithDiscographyJobHandler;
		_logger = logger;
	}

	public async Task Start(Job job, CancellationToken cancellationToken)
	{
		job.MarkAsStarted();
		_logger.LogInformation("{JobType} job {JobId} started", job.Type, job.Id);

		try
		{
			await Execute(job, cancellationToken);
			job.MarkAsCompleted();
			_logger.LogInformation("{JobType} job {JobId} completed", job.Type, job.Id);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			job.MarkAsCancelled();
			_logger.LogWarning("{JobType} job {JobId} cancelled", job.Type, job.Id);
		}
		catch (Exception exception)
		{
			job.MarkAsFailed();
			_logger.LogError(exception, "{JobType} job {JobId} failed", job.Type, job.Id);
		}
	}

	private Task Execute(Job job, CancellationToken cancellationToken)
	{
		return job.Type switch
		{
			JobType.SyncLibrary => _syncLibraryHandler.Execute(cancellationToken),
			JobType.SyncAlbumWithDiscography when job.AlbumId is { } albumId => _syncAlbumWithDiscographyJobHandler.Execute(albumId, cancellationToken),
			JobType.SyncAlbumWithDiscography => throw new InvalidOperationException($"{job.Type} job {job.Id} has no album id"),
			JobType.SyncArtistWithDiscography when job.ArtistId is { } artistId => _syncArtistWithDiscographyJobHandler.Execute(artistId, cancellationToken),
			JobType.SyncArtistWithDiscography => throw new InvalidOperationException($"{job.Type} job {job.Id} has no artist id"),
			_ => throw new InvalidOperationException($"No handler for {job.Type} job {job.Id}"),
		};
	}
}
