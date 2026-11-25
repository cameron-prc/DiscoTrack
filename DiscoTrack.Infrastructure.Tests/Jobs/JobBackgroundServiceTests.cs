using System.Threading.Channels;
using DiscoTrack.Infrastructure.Jobs;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Infrastructure.Tests.Jobs;

public class JobBackgroundServiceTests : IDisposable
{
	private readonly Channel<ISyncLibraryHandler> _executed = Channel.CreateUnbounded<ISyncLibraryHandler>();
	private readonly ServiceProvider _provider;
	private readonly IJobQueue _queue;
	private readonly IHostedService _service;
	private Exception? _handlerThrows;
	private bool _handlerBlocksUntilCancelled;

	public JobBackgroundServiceTests()
	{
		_provider = BuildProvider();
		_queue = _provider.GetRequiredService<IJobQueue>();
		_service = _provider.GetRequiredService<IHostedService>();
	}

	public void Dispose()
	{
		_provider.Dispose();
	}

	[Fact]
	public void AddJobBackgroundService_registers_it_as_the_hosted_service()
	{
		Assert.IsType<JobBackgroundService>(_service);
	}

	[Fact]
	public async Task Runs_an_enqueued_job_to_completion()
	{
		var job = new Job(JobType.SyncLibrary);
		await _service.StartAsync(CancellationToken.None);

		await _queue.Enqueue(job, CancellationToken.None);
		await Next(_executed.Reader);
		await _service.StopAsync(CancellationToken.None);

		Assert.Equal(JobStatus.Completed, job.Status);
	}

	[Fact]
	public async Task Runs_a_job_enqueued_before_the_service_started()
	{
		var job = new Job(JobType.SyncLibrary);
		await _queue.Enqueue(job, CancellationToken.None);

		await _service.StartAsync(CancellationToken.None);
		await Next(_executed.Reader);
		await _service.StopAsync(CancellationToken.None);

		Assert.Equal(JobStatus.Completed, job.Status);
	}

	[Fact]
	public async Task Each_job_runs_in_its_own_scope()
	{
		await _service.StartAsync(CancellationToken.None);

		await _queue.Enqueue(new Job(JobType.SyncLibrary), CancellationToken.None);
		await _queue.Enqueue(new Job(JobType.SyncLibrary), CancellationToken.None);
		var first = await Next(_executed.Reader);
		var second = await Next(_executed.Reader);
		await _service.StopAsync(CancellationToken.None);

		Assert.NotSame(first, second);
	}

	[Fact]
	public async Task A_failing_job_does_not_stop_the_service()
	{
		var failing = new Job(JobType.SyncLibrary);
		var next = new Job(JobType.SyncLibrary);
		_handlerThrows = new InvalidOperationException("boom");
		await _service.StartAsync(CancellationToken.None);

		await _queue.Enqueue(failing, CancellationToken.None);
		await Next(_executed.Reader);
		_handlerThrows = null;
		await _queue.Enqueue(next, CancellationToken.None);
		await Next(_executed.Reader);
		await _service.StopAsync(CancellationToken.None);

		Assert.Equal(JobStatus.Failed, failing.Status);
		Assert.Equal(JobStatus.Completed, next.Status);
	}

	[Fact]
	public async Task A_runner_that_throws_does_not_stop_the_service()
	{
		var failing = new Job(JobType.SyncLibrary);
		var next = new Job(JobType.SyncLibrary);
		var started = Channel.CreateUnbounded<Job>();
		var runner = new Mock<IJobRunner>();
		runner
			.Setup(r => r.Start(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
			.Returns((Job job, CancellationToken _) =>
			{
				started.Writer.TryWrite(job);
				if (job == failing)
				{
					throw new InvalidOperationException("boom");
				}
				return Task.CompletedTask;
			});
		using var provider = BuildProvider(services => services.AddSingleton(runner.Object));
		var queue = provider.GetRequiredService<IJobQueue>();
		var service = provider.GetRequiredService<IHostedService>();
		await service.StartAsync(CancellationToken.None);

		await queue.Enqueue(failing, CancellationToken.None);
		await queue.Enqueue(next, CancellationToken.None);
		Assert.Same(failing, await Next(started.Reader));
		Assert.Same(next, await Next(started.Reader));
		await service.StopAsync(CancellationToken.None);

		var executeTask = Assert.IsType<JobBackgroundService>(service).ExecuteTask;
		Assert.NotNull(executeTask);
		Assert.False(executeTask.IsFaulted);
	}

	[Fact]
	public async Task Stopping_the_service_cancels_the_job_in_progress()
	{
		var job = new Job(JobType.SyncLibrary);
		_handlerBlocksUntilCancelled = true;
		await _service.StartAsync(CancellationToken.None);

		await _queue.Enqueue(job, CancellationToken.None);
		await Next(_executed.Reader);
		await _service.StopAsync(CancellationToken.None);

		Assert.Equal(JobStatus.Cancelled, job.Status);
	}

	[Fact]
	public async Task Stops_cleanly_with_nothing_queued()
	{
		await _service.StartAsync(CancellationToken.None);

		await _service.StopAsync(CancellationToken.None);

		var executeTask = Assert.IsType<JobBackgroundService>(_service).ExecuteTask;
		Assert.NotNull(executeTask);
		Assert.True(executeTask.IsCompleted);
	}

	private ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
	{
		var services = new ServiceCollection()
			.AddInMemoryJobQueue()
			.AddScoped(_ => MockHandler())
			.AddScoped(_ => Mock.Of<ISyncAlbumWithDiscographyJobHandler>())
			.AddScoped(_ => Mock.Of<ISyncArtistWithDiscographyJobHandler>())
			.AddScoped<IJobRunner, JobRunner>()
			.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
			.AddJobBackgroundService();
		configure?.Invoke(services);
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
	}

	private ISyncLibraryHandler MockHandler()
	{
		var handler = new Mock<ISyncLibraryHandler>();
		handler
			.Setup(h => h.Execute(It.IsAny<CancellationToken>()))
			.Returns((CancellationToken ct) => Execute(handler.Object, ct));
		return handler.Object;
	}

	private Task Execute(ISyncLibraryHandler handler, CancellationToken ct)
	{
		_executed.Writer.TryWrite(handler);
		if (_handlerThrows is not null)
		{
			throw _handlerThrows;
		}
		if (_handlerBlocksUntilCancelled)
		{
			return Task.Delay(Timeout.Infinite, ct);
		}
		return Task.CompletedTask;
	}

	private static async Task<T> Next<T>(ChannelReader<T> reader)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		return await reader.ReadAsync(timeout.Token);
	}
}
