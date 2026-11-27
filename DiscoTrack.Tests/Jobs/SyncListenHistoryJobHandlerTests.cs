using DiscoTrack.Common;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using DiscoTrack.ExternalIds;
using DiscoTrack.ListenHistory;
using DiscoTrack.ListenHistory.ResolvePlays;
using DiscoTrack.ListenHistory.SyncListenHistory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class SyncListenHistoryJobHandlerTests
{
	private readonly Mock<ISyncListenHistoryService> _service = new();
	private readonly Mock<IResolvePlaysService> _resolvePlays = new();

	public SyncListenHistoryJobHandlerTests()
	{
		_resolvePlays.Setup(resolve => resolve.Run(It.IsAny<IReadOnlyList<Play>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ResolvePlaysResponse([], [], []));
	}

	private SyncListenHistoryJobHandler Handler()
	{
		return new SyncListenHistoryJobHandler(_service.Object, _resolvePlays.Object, NullLogger<SyncListenHistoryJobHandler>.Instance);
	}

	[Fact]
	public async Task Resolves_the_plays_the_sync_added()
	{
		using var cts = new CancellationTokenSource();
		IReadOnlyList<Play> added = [new Play { PlayedAt = DateTimeOffset.UnixEpoch, TrackId = new SpotifyTrackId("trk-1"), TrackTitle = "Genesis" }];
		_service.Setup(service => service.Run(cts.Token)).ReturnsAsync(new SyncListenHistoryResponse(FetchedPlays: 3, AddedPlays: added, After: null));

		await Handler().Execute(cts.Token);

		_resolvePlays.Verify(resolve => resolve.Run(added, cts.Token), Times.Once);
	}

	[Fact]
	public async Task Fails_the_job_when_the_added_plays_cannot_be_resolved()
	{
		_service.Setup(service => service.Run(It.IsAny<CancellationToken>())).ReturnsAsync(new SyncListenHistoryResponse(0, [], null));
		_resolvePlays.Setup(resolve => resolve.Run(It.IsAny<IReadOnlyList<Play>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<IResolvePlaysError> { new UnableToReadCatalogueError("database is locked") });

		await Assert.ThrowsAsync<JobFailedException>(() => Handler().Execute(CancellationToken.None));
	}

	[Fact]
	public async Task Does_not_resolve_anything_when_the_sync_fails()
	{
		_service.Setup(service => service.Run(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<ISyncListenHistoryError> { new UnableToReadListenHistorySourceError("Spotify is not connected.") });

		await Assert.ThrowsAsync<JobFailedException>(() => Handler().Execute(CancellationToken.None));

		_resolvePlays.Verify(resolve => resolve.Run(It.IsAny<IReadOnlyList<Play>>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Runs_the_listen_history_sync_with_the_jobs_cancellation_token()
	{
		using var cts = new CancellationTokenSource();
		_service.Setup(service => service.Run(cts.Token))
			.ReturnsAsync(new SyncListenHistoryResponse(FetchedPlays: 3, AddedPlays: [], After: null));

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
