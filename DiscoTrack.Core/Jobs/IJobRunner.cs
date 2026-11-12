namespace DiscoTrack.Core.Jobs;

public interface IJobRunner
{
	Task Start(Job job, CancellationToken cancellationToken);
}
