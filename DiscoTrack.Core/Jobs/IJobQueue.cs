namespace DiscoTrack.Core.Jobs;

public interface IJobQueue
{
	Task Enqueue(Job job, CancellationToken ct);
	IAsyncEnumerable<Job> Dequeue(CancellationToken ct);
}
