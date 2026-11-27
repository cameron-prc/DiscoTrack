using DiscoTrack.ExternalIds;
using DiscoTrack.Playlists;
using DiscoTrack.Playlists.SyncPlaylists;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Playlists;

public class SyncPlaylistsServiceTests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 27, 8, 0, 0, TimeSpan.Zero);
	private static readonly SpotifyPlaylistId RoadTrip = new("pl-road-trip");
	private static readonly SpotifyPlaylistId Gym = new("pl-gym");

	private readonly Mock<IPlaylistSource> _source = new();
	private readonly Mock<IPlaylists> _playlists = new();
	private readonly Mock<TimeProvider> _time = new();
	private readonly List<Playlist> _known = [];
	private readonly List<Playlist> _saved = [];

	public SyncPlaylistsServiceTests()
	{
		_source.SetupGet(source => source.PlaylistProvider).Returns(Provider.SpotifyPlaylist);
		_playlists.Setup(playlists => playlists.GetPlaylists(It.IsAny<CancellationToken>())).ReturnsAsync(() => _known.ToList());
		_playlists.Setup(playlists => playlists.SavePlaylist(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()))
			.Callback((Playlist playlist, CancellationToken _) => _saved.Add(playlist));
		_time.Setup(time => time.GetUtcNow()).Returns(Now);
	}

	private SyncPlaylistsService Sync()
	{
		return new SyncPlaylistsService(_source.Object, _playlists.Object, _time.Object, NullLogger<SyncPlaylistsService>.Instance);
	}

	[Fact]
	public async Task Updates_a_tracked_playlist_whose_description_or_marker_changed_and_keeps_what_is_ours()
	{
		var tracked = Tracked(RoadTrip, "Road Trip", "snap-1");
		tracked.ImportedSnapshotId = "snap-1";
		tracked.LastImportedAt = Now.AddDays(-1);
		SourceHas(new ProviderPlaylist(RoadTrip, "Road Trip 2026", null, "friend", "snap-2", 43));

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Same(tracked, Assert.Single(_saved));
		Assert.Equal(("Road Trip 2026", "friend", "snap-2", 43), (tracked.Name, tracked.OwnerName, tracked.SnapshotId, tracked.ItemCount));
		Assert.True(tracked.Tracked);
		Assert.Equal("snap-1", tracked.ImportedSnapshotId);
		Assert.True(tracked.HasChangedSinceImport);
		Assert.Equal([tracked], response.Playlists);
		Assert.Empty(response.RemovedPlaylists);
	}

	[Fact]
	public async Task Saves_nothing_for_a_playlist_that_has_not_changed()
	{
		var tracked = Tracked(RoadTrip, "Road Trip", "snap-1");
		SourceHas(new ProviderPlaylist(RoadTrip, "Road Trip", null, "cam", "snap-1", 10));

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Empty(_saved);
		Assert.Equal([tracked], response.Playlists);
	}

	[Fact]
	public async Task Marks_a_playlist_the_source_no_longer_has_as_removed_and_keeps_it()
	{
		var gone = Tracked(Gym, "Gym", "snap-9");

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Equal(Now, gone.RemovedAt);
		Assert.Same(gone, Assert.Single(_saved));
		Assert.Equal([gone], response.RemovedPlaylists);
		Assert.Equal([gone], response.Playlists);
	}

	[Fact]
	public async Task Does_not_save_a_playlist_already_marked_removed_again()
	{
		var gone = Tracked(Gym, "Gym", "snap-9");
		gone.RemovedAt = Now.AddDays(-3);

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Empty(_saved);
		Assert.Empty(response.RemovedPlaylists);
		Assert.Equal(Now.AddDays(-3), gone.RemovedAt);
	}

	[Fact]
	public async Task Unmarks_a_removed_playlist_the_source_has_again()
	{
		var back = Tracked(Gym, "Gym", "snap-9");
		back.RemovedAt = Now.AddDays(-3);
		SourceHas(new ProviderPlaylist(Gym, "Gym", null, "cam", "snap-9", 10));

		await Sync().Run(CancellationToken.None);

		Assert.Null(back.RemovedAt);
		Assert.Same(back, Assert.Single(_saved));
	}

	[Fact]
	public async Task Leaves_untracked_playlists_and_those_of_another_provider_alone()
	{
		var untracked = Tracked(Gym, "Gym", "snap-9");
		untracked.Tracked = false;
		_known.Add(new Playlist { ExternalId = new SubsonicId("pl-navidrome"), Name = "Navidrome mix", Tracked = true });

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Empty(response.Playlists);
		Assert.Empty(_saved);
		Assert.Null(untracked.RemovedAt);
		_source.Verify(source => source.GetPlaylist(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Returns_an_error_when_the_playlists_cannot_be_read()
	{
		_playlists.Setup(playlists => playlists.GetPlaylists(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal(new UnableToReadPlaylistsError("database is locked"), Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Returns_an_error_and_saves_nothing_when_the_source_cannot_be_read()
	{
		Tracked(RoadTrip, "Road Trip", "snap-1");
		SourceHas(new ProviderPlaylist(RoadTrip, "Road Trip 2026", null, "cam", "snap-2", 10));
		Tracked(Gym, "Gym", "snap-9");
		_source.Setup(source => source.GetPlaylist(Gym, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not connected"));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal(new UnableToReadPlaylistSourceError("not connected"), Assert.Single(result.GetLeft));
		Assert.Empty(_saved);
	}

	[Fact]
	public async Task Returns_an_error_when_a_playlist_cannot_be_saved()
	{
		Tracked(RoadTrip, "Road Trip", "snap-1");
		SourceHas(new ProviderPlaylist(RoadTrip, "Road Trip", null, "cam", "snap-2", 10));
		_playlists.Setup(playlists => playlists.SavePlaylist(It.IsAny<Playlist>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("disk full"));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal(new UnableToSavePlaylistError("disk full"), Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_becoming_an_error()
	{
		Tracked(RoadTrip, "Road Trip", "snap-1");
		_source.Setup(source => source.GetPlaylist(RoadTrip, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => Sync().Run(CancellationToken.None));
	}

	private void SourceHas(ProviderPlaylist providerPlaylist)
	{
		_source.Setup(source => source.GetPlaylist(providerPlaylist.Id, It.IsAny<CancellationToken>())).ReturnsAsync(providerPlaylist);
	}

	private Playlist Tracked(ExternalId id, string name, string snapshotId)
	{
		var playlist = new Playlist { ExternalId = id, Name = name, OwnerName = "cam", SnapshotId = snapshotId, ItemCount = 10, Tracked = true };
		_known.Add(playlist);

		return playlist;
	}
}
