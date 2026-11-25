using DiscoTrack.Artists;
using DiscoTrack.Common;
using DiscoTrack.Discography.SyncArtistWithDiscography;
using DiscoTrack.Jobs.JobHandlers;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class SyncArtistWithDiscographyJobHandlerTests
{
	private readonly Mock<ISyncArtistWithDiscographyService> _sync = new();
	private readonly Mock<ILogger<SyncArtistWithDiscographyJobHandler>> _logger = new();
	private readonly ArtistId _artistId = ArtistId.New();

	private SyncArtistWithDiscographyJobHandler Handler()
	{
		return new SyncArtistWithDiscographyJobHandler(_sync.Object, _logger.Object);
	}

	[Fact]
	public async Task Syncs_the_artist_and_logs_the_outcome()
	{
		using var cts = new CancellationTokenSource();
		var artist = new Artist { Id = _artistId, Name = "Tool" };
		_sync.Setup(sync => sync.Run(_artistId, cts.Token))
			.ReturnsAsync(new SyncArtistWithDiscographyResponse(artist, [], [], [], [], []));

		await Handler().Execute(_artistId, cts.Token);

		_sync.Verify(sync => sync.Run(_artistId, cts.Token), Times.Once);
		VerifyLogged(LogLevel.Information, Times.Once());
		VerifyLogged(LogLevel.Error, Times.Never());
	}

	[Fact]
	public async Task Logs_each_error_the_sync_returns()
	{
		_sync.Setup(sync => sync.Run(_artistId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Either<IList<ISyncArtistWithDiscographyError>, SyncArtistWithDiscographyResponse>.Left(
				[new UnableToSaveArtistError("database is locked"), new UnableToFindDiscographyArtistError()]));

		await Handler().Execute(_artistId, CancellationToken.None);

		VerifyLogged(LogLevel.Error, Times.Exactly(2));
		VerifyLogged(LogLevel.Information, Times.Never());
	}

	private void VerifyLogged(LogLevel level, Times times)
	{
		_logger.Verify(
			logger => logger.Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			times);
	}
}
