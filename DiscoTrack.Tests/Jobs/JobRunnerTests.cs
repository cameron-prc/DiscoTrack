using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using DiscoTrack.Playlists;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Jobs;

public class JobRunnerTests
{
	private readonly Mock<ISyncLibraryHandler> _handler = new();
	private readonly Mock<ISyncAlbumWithDiscographyJobHandler> _discographyHandler = new();
	private readonly Mock<ISyncArtistWithDiscographyJobHandler> _artistDiscographyHandler = new();
	private readonly Mock<ISyncAllArtistsWithDiscographyJobHandler> _allArtistsDiscographyHandler = new();
	private readonly Mock<ISyncListenHistoryJobHandler> _listenHistoryHandler = new();
	private readonly Mock<ISyncPlaylistsJobHandler> _playlistsHandler = new();
	private readonly Mock<ISyncPlaylistJobHandler> _playlistHandler = new();

	private JobRunner Runner()
	{
		return new JobRunner(_handler.Object, _discographyHandler.Object, _artistDiscographyHandler.Object, _allArtistsDiscographyHandler.Object, _listenHistoryHandler.Object, _playlistsHandler.Object, _playlistHandler.Object, NullLogger<JobRunner>.Instance);
	}

	[Fact]
	public async Task Runs_the_sync_discography_album_handler_with_the_jobs_album_id()
	{
		var albumId = AlbumId.New();
		using var cts = new CancellationTokenSource();

		await Runner().Start(new Job(JobType.SyncAlbumWithDiscography, albumId), cts.Token);

		_discographyHandler.Verify(h => h.Execute(albumId, cts.Token), Times.Once);
		_handler.Verify(h => h.Execute(It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Fails_a_sync_discography_album_job_that_has_no_album_id()
	{
		var job = new Job(JobType.SyncAlbumWithDiscography);

		await Runner().Start(job, CancellationToken.None);

		Assert.Equal(JobStatus.Failed, job.Status);
		_discographyHandler.Verify(h => h.Execute(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Runs_the_sync_discography_artist_handler_with_the_jobs_artist_id()
	{
		var artistId = ArtistId.New();
		using var cts = new CancellationTokenSource();

		await Runner().Start(new Job(JobType.SyncArtistWithDiscography, artistId), cts.Token);

		_artistDiscographyHandler.Verify(h => h.Execute(artistId, cts.Token), Times.Once);
		_discographyHandler.Verify(h => h.Execute(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Fails_a_sync_discography_artist_job_that_has_no_artist_id()
	{
		var job = new Job(JobType.SyncArtistWithDiscography);

		await Runner().Start(job, CancellationToken.None);

		Assert.Equal(JobStatus.Failed, job.Status);
		_artistDiscographyHandler.Verify(h => h.Execute(It.IsAny<ArtistId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Runs_the_sync_all_artists_handler_for_a_sync_all_artists_job()
	{
		using var cts = new CancellationTokenSource();

		await Runner().Start(new Job(JobType.SyncAllArtistsWithDiscography), cts.Token);

		_allArtistsDiscographyHandler.Verify(h => h.Execute(cts.Token), Times.Once);
		_artistDiscographyHandler.Verify(h => h.Execute(It.IsAny<ArtistId>(), It.IsAny<CancellationToken>()), Times.Never);
		_handler.Verify(h => h.Execute(It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Runs_the_sync_listen_history_handler_for_a_sync_listen_history_job()
	{
		using var cts = new CancellationTokenSource();

		await Runner().Start(new Job(JobType.SyncListenHistory), cts.Token);

		_listenHistoryHandler.Verify(h => h.Execute(cts.Token), Times.Once);
		_handler.Verify(h => h.Execute(It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Runs_the_sync_playlists_handler_for_a_sync_playlists_job()
	{
		using var cts = new CancellationTokenSource();

		await Runner().Start(new Job(JobType.SyncPlaylists), cts.Token);

		_playlistsHandler.Verify(h => h.Execute(cts.Token), Times.Once);
		_handler.Verify(h => h.Execute(It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Runs_the_sync_playlist_handler_with_the_jobs_playlist_id()
	{
		var playlistId = PlaylistId.New();
		using var cts = new CancellationTokenSource();

		await Runner().Start(new Job(JobType.SyncPlaylist, playlistId), cts.Token);

		_playlistHandler.Verify(h => h.Execute(playlistId, cts.Token), Times.Once);
		_playlistsHandler.Verify(h => h.Execute(It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Fails_a_sync_playlist_job_that_has_no_playlist_id()
	{
		var job = new Job(JobType.SyncPlaylist);

		await Runner().Start(job, CancellationToken.None);

		Assert.Equal(JobStatus.Failed, job.Status);
		_playlistHandler.Verify(h => h.Execute(It.IsAny<PlaylistId>(), It.IsAny<CancellationToken>()), Times.Never);
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
