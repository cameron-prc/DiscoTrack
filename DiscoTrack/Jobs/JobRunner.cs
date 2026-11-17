using DiscoTrack.Jobs.JobHandlers;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs;

public class JobRunner : IJobRunner
{
	private readonly IJobHandler _syncLibraryHandler;
	private readonly ILogger<JobRunner> _logger;

	public JobRunner(ISyncLibraryHandler syncLibraryHandler, ILogger<JobRunner> logger)
	{
		_syncLibraryHandler = syncLibraryHandler;
		_logger = logger;
	}

	public async Task Start(Job job, CancellationToken cancellationToken)
	{
		var handler = GetHandler(job.Type);

		job.MarkAsStarted();
		_logger.LogInformation("{JobType} job {JobId} started", job.Type, job.Id);

		try
		{
			await handler.Execute(cancellationToken);
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

	private IJobHandler GetHandler(JobType jobType)
	{
		return jobType switch
		{
			JobType.SyncLibrary => _syncLibraryHandler
		};
	}
}
