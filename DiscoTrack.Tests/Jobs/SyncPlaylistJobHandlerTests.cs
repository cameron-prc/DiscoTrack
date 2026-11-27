using DiscoTrack.ExternalIds;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using DiscoTrack.Playlists;
using DiscoTrack.Playlists.ImportPlaylist;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class SyncPlaylistJobHandlerTests
{
	private readonly Mock<IImportPlaylistService> _importPlaylist = new();
	private readonly Playlist _playlist = new() { ExternalId = new SpotifyPlaylistId("pl-road-trip"), Name = "Road Trip" };

	private SyncPlaylistJobHandler Handler()
	{
		return new SyncPlaylistJobHandler(_importPlaylist.Object, NullLogger<SyncPlaylistJobHandler>.Instance);
	}

	[Fact]
	public async Task Copies_the_playlist_whether_or_not_it_has_changed()
	{
		using var cts = new CancellationTokenSource();
		_importPlaylist.Setup(import => import.Run(_playlist.Id, false, cts.Token)).ReturnsAsync(new ImportPlaylistResponse(_playlist, true, 2, 2, 0, []));

		await Handler().Execute(_playlist.Id, cts.Token);

		_importPlaylist.Verify(import => import.Run(_playlist.Id, false, cts.Token), Times.Once);
	}

	[Fact]
	public async Task Fails_the_job_when_the_import_returns_errors()
	{
		_importPlaylist.Setup(import => import.Run(_playlist.Id, false, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<IImportPlaylistError> { new UnableToFindPlaylistError() });

		await Assert.ThrowsAsync<JobFailedException>(() => Handler().Execute(_playlist.Id, CancellationToken.None));
	}
}
