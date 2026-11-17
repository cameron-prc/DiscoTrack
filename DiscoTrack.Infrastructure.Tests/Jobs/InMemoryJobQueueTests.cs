using DiscoTrack.Infrastructure.Jobs;
using DiscoTrack.Jobs;

namespace DiscoTrack.Infrastructure.Tests.Jobs;

public class InMemoryJobQueueTests
{
	private readonly InMemoryJobQueue _queue = new();

	[Fact]
	public async Task Dequeues_jobs_in_the_order_they_were_enqueued()
	{
		var first = new Job(JobType.SyncLibrary);
		var second = new Job(JobType.SyncLibrary);
		await _queue.Enqueue(first, CancellationToken.None);
		await _queue.Enqueue(second, CancellationToken.None);

		var dequeued = new List<Job>();
		await foreach (var job in _queue.Dequeue(CancellationToken.None))
		{
			dequeued.Add(job);
			if (dequeued.Count == 2)
			{
				break;
			}
		}

		Assert.Equal([first, second], dequeued);
	}

	[Fact]
	public async Task Dequeue_waits_for_a_job_enqueued_later()
	{
		var job = new Job(JobType.SyncLibrary);
		await using var reading = _queue.Dequeue(CancellationToken.None).GetAsyncEnumerator();
		var next = reading.MoveNextAsync();

		await _queue.Enqueue(job, CancellationToken.None);

		Assert.True(await next);
		Assert.Same(job, reading.Current);
	}

	[Fact]
	public async Task Dequeue_stops_when_cancelled()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
		{
			await foreach (var _ in _queue.Dequeue(cts.Token))
			{
			}
		});
	}
}
