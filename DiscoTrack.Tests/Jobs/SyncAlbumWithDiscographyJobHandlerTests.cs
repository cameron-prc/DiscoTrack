using DiscoTrack.Albums;
using DiscoTrack.Common;
using DiscoTrack.Discography.SyncAlbumWithDiscography;
using DiscoTrack.Jobs.JobHandlers;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class SyncAlbumWithDiscographyJobHandlerTests
{
	private readonly Mock<ISyncAlbumWithDiscographyService> _sync = new();
	private readonly Mock<ILogger<SyncAlbumWithDiscographyJobHandler>> _logger = new();
	private readonly AlbumId _albumId = AlbumId.New();

	private SyncAlbumWithDiscographyJobHandler Handler()
	{
		return new SyncAlbumWithDiscographyJobHandler(_sync.Object, _logger.Object);
	}

	[Fact]
	public async Task Syncs_the_album_and_logs_the_outcome()
	{
		using var cts = new CancellationTokenSource();
		var album = new Album { Id = _albumId, Title = "Lateralus", Year = 2001 };
		_sync.Setup(sync => sync.Run(_albumId, cts.Token))
			.ReturnsAsync(new SyncAlbumWithDiscographyResponse(album, [], [], [], [], null));

		await Handler().Execute(_albumId, cts.Token);

		_sync.Verify(sync => sync.Run(_albumId, cts.Token), Times.Once);
		VerifyLogged(LogLevel.Information, Times.Once());
		VerifyLogged(LogLevel.Error, Times.Never());
	}

	[Fact]
	public async Task Logs_each_error_the_sync_returns()
	{
		_sync.Setup(sync => sync.Run(_albumId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Either<IList<ISyncAlbumWithDiscographyError>, SyncAlbumWithDiscographyResponse>.Left(
				[new UnableToSaveArtistError("database is locked"), new UnableToFindDiscographyAlbumError()]));

		await Handler().Execute(_albumId, CancellationToken.None);

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
