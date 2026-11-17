using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class JobRunnerTests
{
	private readonly Mock<ISyncLibraryHandler> _handler = new();

	private JobRunner Runner()
	{
		return new JobRunner(_handler.Object, NullLogger<JobRunner>.Instance);
	}

	[Fact]
	public async Task Runs_the_sync_library_handler_for_a_sync_library_job()
	{
		await Runner().Start(new Job(JobType.SyncLibrary), CancellationToken.None);

		_handler.Verify(h => h.Execute(It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Passes_the_cancellation_token_to_the_handler()
	{
		using var cts = new CancellationTokenSource();

		await Runner().Start(new Job(JobType.SyncLibrary), cts.Token);

		_handler.Verify(h => h.Execute(cts.Token), Times.Once);
	}

	[Fact]
	public async Task Job_is_running_while_the_handler_executes()
	{
		var job = new Job(JobType.SyncLibrary);
		JobStatus? statusDuringExecute = null;
		_handler
			.Setup(h => h.Execute(It.IsAny<CancellationToken>()))
			.Callback(() => statusDuringExecute = job.Status)
			.Returns(Task.CompletedTask);

		await Runner().Start(job, CancellationToken.None);

		Assert.Equal(JobStatus.Running, statusDuringExecute);
		Assert.NotNull(job.StartedAt);
	}

	[Fact]
	public async Task Completes_the_job_when_the_handler_succeeds()
	{
		var job = new Job(JobType.SyncLibrary);

		await Runner().Start(job, CancellationToken.None);

		Assert.Equal(JobStatus.Completed, job.Status);
		Assert.NotNull(job.FinishedAt);
	}

	[Fact]
	public async Task Fails_the_job_and_swallows_the_exception_when_the_handler_throws()
	{
		var job = new Job(JobType.SyncLibrary);
		_handler
			.Setup(h => h.Execute(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("boom"));

		await Runner().Start(job, CancellationToken.None);

		Assert.Equal(JobStatus.Failed, job.Status);
		Assert.NotNull(job.FinishedAt);
	}

	[Fact]
	public async Task Cancels_the_job_when_the_token_is_cancelled_during_the_handler()
	{
		var job = new Job(JobType.SyncLibrary);
		using var cts = new CancellationTokenSource();
		_handler
			.Setup(h => h.Execute(cts.Token))
			.Callback(cts.Cancel)
			.ThrowsAsync(new OperationCanceledException(cts.Token));

		await Runner().Start(job, cts.Token);

		Assert.Equal(JobStatus.Cancelled, job.Status);
		Assert.NotNull(job.FinishedAt);
	}

	[Fact]
	public async Task Fails_the_job_when_the_handler_throws_cancellation_without_the_token_being_cancelled()
	{
		var job = new Job(JobType.SyncLibrary);
		_handler
			.Setup(h => h.Execute(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());

		await Runner().Start(job, CancellationToken.None);

		Assert.Equal(JobStatus.Failed, job.Status);
	}
}
