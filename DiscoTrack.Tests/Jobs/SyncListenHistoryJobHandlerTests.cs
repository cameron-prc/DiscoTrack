using DiscoTrack.Common;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using DiscoTrack.ListenHistory.SyncListenHistory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class SyncListenHistoryJobHandlerTests
{
	private readonly Mock<ISyncListenHistoryService> _service = new();

	private SyncListenHistoryJobHandler Handler()
	{
		return new SyncListenHistoryJobHandler(_service.Object, NullLogger<SyncListenHistoryJobHandler>.Instance);
	}

	[Fact]
	public async Task Runs_the_listen_history_sync_with_the_jobs_cancellation_token()
	{
		using var cts = new CancellationTokenSource();
		_service.Setup(service => service.Run(cts.Token))
			.ReturnsAsync(new SyncListenHistoryResponse(FetchedPlays: 3, AddedPlays: 2, After: null));

		await Handler().Execute(cts.Token);

		_service.Verify(service => service.Run(cts.Token), Times.Once);
	}

	[Fact]
	public async Task Fails_the_job_when_the_sync_returns_errors()
	{
		_service.Setup(service => service.Run(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<ISyncListenHistoryError> { new UnableToReadListenHistorySourceError("Spotify is not connected.") });

		var exception = await Assert.ThrowsAsync<JobFailedException>(() => Handler().Execute(CancellationToken.None));

		Assert.Equal("Listen history sync failed with 1 errors", exception.Message);
	}
}
