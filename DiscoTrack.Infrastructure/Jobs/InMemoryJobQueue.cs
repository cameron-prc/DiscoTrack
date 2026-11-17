using System.Threading.Channels;
using DiscoTrack.Jobs;

namespace DiscoTrack.Infrastructure.Jobs;

public sealed class InMemoryJobQueue : IJobQueue
{
	private readonly Channel<Job> _channel = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = true });

	public Task Enqueue(Job job, CancellationToken ct)
	{
		_channel.Writer.TryWrite(job);

		return Task.CompletedTask;
	}

	public IAsyncEnumerable<Job> Dequeue(CancellationToken ct)
	{
		return _channel.Reader.ReadAllAsync(ct);
	}
}
