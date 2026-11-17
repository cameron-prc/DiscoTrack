namespace DiscoTrack.Jobs;

public interface IJobRunner
{
	Task Start(Job job, CancellationToken cancellationToken);
}
