using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class SyncAllArtistsWithDiscographyJobHandlerTests
{
	private readonly Mock<ICatalogue> _catalogue = new();
	private readonly Mock<ISyncArtistWithDiscographyJobHandler> _syncArtist = new();
	private readonly Artist _tool = new() { Name = "Tool" };
	private readonly Artist _puscifer = new() { Name = "Puscifer" };

	public SyncAllArtistsWithDiscographyJobHandlerTests()
	{
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([_tool, _puscifer]);
	}

	private SyncAllArtistsWithDiscographyJobHandler Handler()
	{
		return new SyncAllArtistsWithDiscographyJobHandler(_catalogue.Object, _syncArtist.Object, NullLogger<SyncAllArtistsWithDiscographyJobHandler>.Instance);
	}

	[Fact]
	public async Task Syncs_every_catalogue_artist_in_order()
	{
		using var cts = new CancellationTokenSource();
		var synced = new List<ArtistId>();
		_syncArtist.Setup(syncArtist => syncArtist.Execute(It.IsAny<ArtistId>(), cts.Token))
			.Callback((ArtistId artistId, CancellationToken _) => synced.Add(artistId))
			.Returns(Task.CompletedTask);

		await Handler().Execute(cts.Token);

		Assert.Equal([_tool.Id, _puscifer.Id], synced);
	}

	[Fact]
	public async Task Carries_on_with_the_next_artist_when_one_fails()
	{
		_syncArtist.Setup(syncArtist => syncArtist.Execute(_tool.Id, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new JobFailedException("Discography sync for artist failed"));

		await Handler().Execute(CancellationToken.None);

		_syncArtist.Verify(syncArtist => syncArtist.Execute(_puscifer.Id, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Stops_when_cancelled()
	{
		_syncArtist.Setup(syncArtist => syncArtist.Execute(_tool.Id, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => Handler().Execute(CancellationToken.None));

		_syncArtist.Verify(syncArtist => syncArtist.Execute(_puscifer.Id, It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Fails_when_the_catalogue_cannot_be_read()
	{
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Execute(CancellationToken.None));

		_syncArtist.Verify(syncArtist => syncArtist.Execute(It.IsAny<ArtistId>(), It.IsAny<CancellationToken>()), Times.Never);
	}
}
