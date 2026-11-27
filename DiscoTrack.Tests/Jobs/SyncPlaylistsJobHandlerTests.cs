using DiscoTrack.ExternalIds;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using DiscoTrack.Playlists;
using DiscoTrack.Playlists.ImportPlaylist;
using DiscoTrack.Playlists.SyncPlaylists;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class SyncPlaylistsJobHandlerTests
{
	private readonly Mock<ISyncPlaylistsService> _syncPlaylists = new();
	private readonly Mock<IImportPlaylistService> _importPlaylist = new();
	private readonly List<Playlist> _playlists = [];

	public SyncPlaylistsJobHandlerTests()
	{
		_syncPlaylists.Setup(sync => sync.Run(It.IsAny<CancellationToken>())).ReturnsAsync(() => new SyncPlaylistsResponse(_playlists.ToList(), []));
		_importPlaylist.Setup(import => import.Run(It.IsAny<PlaylistId>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((PlaylistId playlistId, bool _, CancellationToken _) => new ImportPlaylistResponse(_playlists.Single(playlist => playlist.Id == playlistId), true, 0, 0, 0, []));
	}

	private SyncPlaylistsJobHandler Handler()
	{
		return new SyncPlaylistsJobHandler(_syncPlaylists.Object, _importPlaylist.Object, NullLogger<SyncPlaylistsJobHandler>.Instance);
	}

	[Fact]
	public async Task Updates_each_refreshed_playlist_the_provider_still_has_only_when_it_has_changed()
	{
		var tracked = Playlist("pl-road-trip", tracked: true);
		var removed = Playlist("pl-old", tracked: true);
		removed.RemovedAt = DateTimeOffset.UnixEpoch;
		using var cts = new CancellationTokenSource();

		await Handler().Execute(cts.Token);

		_importPlaylist.Verify(import => import.Run(tracked.Id, true, cts.Token), Times.Once);
		_importPlaylist.Verify(import => import.Run(removed.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Fails_without_updating_any_playlist_when_the_playlists_cannot_be_refreshed()
	{
		Playlist("pl-road-trip", tracked: true);
		_syncPlaylists.Setup(sync => sync.Run(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<ISyncPlaylistsError> { new UnableToReadPlaylistSourceError("not connected") });

		await Assert.ThrowsAsync<JobFailedException>(() => Handler().Execute(CancellationToken.None));

		_importPlaylist.Verify(import => import.Run(It.IsAny<PlaylistId>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Carries_on_with_the_other_playlists_when_one_fails_and_then_fails_the_job()
	{
		var failing = Playlist("pl-road-trip", tracked: true);
		var fine = Playlist("pl-gym", tracked: true);
		_importPlaylist.Setup(import => import.Run(failing.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<IImportPlaylistError> { new UnableToReadPlaylistItemsFromSourceError("not connected") });

		await Assert.ThrowsAsync<JobFailedException>(() => Handler().Execute(CancellationToken.None));

		_importPlaylist.Verify(import => import.Run(fine.Id, true, It.IsAny<CancellationToken>()), Times.Once);
	}

	private Playlist Playlist(string id, bool tracked)
	{
		var playlist = new Playlist { ExternalId = new SpotifyPlaylistId(id), Name = id, Tracked = tracked };
		_playlists.Add(playlist);

		return playlist;
	}
}
