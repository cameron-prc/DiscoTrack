using DiscoTrack.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Infrastructure.Jobs;

public sealed class JobBackgroundService(IJobQueue jobQueue, IServiceScopeFactory scopeFactory, ILogger<JobBackgroundService> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await foreach (var job in jobQueue.Dequeue(stoppingToken))
		{
			try
			{
				await using var scope = scopeFactory.CreateAsyncScope();
				var runner = scope.ServiceProvider.GetRequiredService<IJobRunner>();
				await runner.Start(job, stoppingToken);
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "{JobType} job {JobId} escaped the runner", job.Type, job.Id);
			}
		}
	}
}
