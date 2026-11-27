using DiscoTrack.ExternalIds;
using DiscoTrack.ListenHistory;
using DiscoTrack.ListenHistory.SyncListenHistory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.ListenHistory;

public class SyncListenHistoryServiceTests
{
	private static readonly DateTimeOffset Latest = new(2026, 11, 27, 8, 0, 0, TimeSpan.Zero);

	private readonly Mock<IListenHistorySource> _source = new();
	private readonly Mock<IListenHistory> _listenHistory = new();

	public SyncListenHistoryServiceTests()
	{
		_source.SetupGet(source => source.TrackProvider).Returns(Provider.SpotifyTrack);
		_source.Setup(source => source.GetPlays(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		_listenHistory.Setup(history => history.AddPlays(It.IsAny<IReadOnlyList<Play>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
	}

	private SyncListenHistoryService Sync()
	{
		return new SyncListenHistoryService(_source.Object, _listenHistory.Object, NullLogger<SyncListenHistoryService>.Instance);
	}

	[Fact]
	public async Task Asks_the_source_for_everything_when_no_play_of_its_provider_has_been_copied()
	{
		_listenHistory.Setup(history => history.GetLatestPlayedAt(Provider.SpotifyTrack, It.IsAny<CancellationToken>())).ReturnsAsync((DateTimeOffset?)null);

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		_source.Verify(source => source.GetPlays(null, It.IsAny<CancellationToken>()), Times.Once);
		Assert.Null(response.After);
	}

	[Fact]
	public async Task Asks_the_source_only_for_plays_after_the_latest_one_copied()
	{
		_listenHistory.Setup(history => history.GetLatestPlayedAt(Provider.SpotifyTrack, It.IsAny<CancellationToken>())).ReturnsAsync(Latest);

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		_source.Verify(source => source.GetPlays(Latest, It.IsAny<CancellationToken>()), Times.Once);
		Assert.Equal(Latest, response.After);
	}

	[Fact]
	public async Task Adds_the_plays_the_source_returned_and_reports_how_many_were_new()
	{
		IReadOnlyList<Play> plays = [Play("trk-1", Latest.AddMinutes(1)), Play("trk-2", Latest.AddMinutes(5))];
		_source.Setup(source => source.GetPlays(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>())).ReturnsAsync(plays);
		_listenHistory.Setup(history => history.AddPlays(plays, It.IsAny<CancellationToken>())).ReturnsAsync([plays[1]]);

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Equal(2, response.FetchedPlays);
		Assert.Equal([plays[1]], response.AddedPlays);
		_listenHistory.Verify(history => history.AddPlays(plays, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Returns_an_error_when_the_latest_copied_play_cannot_be_read_and_does_not_call_the_source()
	{
		_listenHistory.Setup(history => history.GetLatestPlayedAt(It.IsAny<Provider>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal(new UnableToReadListenHistoryError("database is locked"), Assert.Single(result.GetLeft));
		_source.Verify(source => source.GetPlays(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Returns_an_error_when_the_source_cannot_be_read_and_adds_nothing()
	{
		_source.Setup(source => source.GetPlays(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Spotify is not connected."));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal(new UnableToReadListenHistorySourceError("Spotify is not connected."), Assert.Single(result.GetLeft));
		_listenHistory.Verify(history => history.AddPlays(It.IsAny<IReadOnlyList<Play>>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Returns_an_error_when_the_plays_cannot_be_saved()
	{
		_listenHistory.Setup(history => history.AddPlays(It.IsAny<IReadOnlyList<Play>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("disk full"));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal(new UnableToSavePlaysError("disk full"), Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_becoming_an_error()
	{
		_source.Setup(source => source.GetPlays(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => Sync().Run(CancellationToken.None));
	}

	private static Play Play(string trackId, DateTimeOffset playedAt)
	{
		return new Play { PlayedAt = playedAt, TrackId = new SpotifyTrackId(trackId), TrackTitle = trackId };
	}
}
