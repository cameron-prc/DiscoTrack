using DiscoTrack.Albums;
using DiscoTrack.ExternalIds;
using DiscoTrack.Playlists;
using DiscoTrack.Playlists.ImportPlaylist;
using DiscoTrack.Providers;
using DiscoTrack.Providers.ResolveProviderTracks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Playlists;

public class ImportPlaylistServiceTests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 27, 8, 0, 0, TimeSpan.Zero);
	private static readonly SpotifyPlaylistId RoadTrip = new("pl-road-trip");
	private static readonly ProviderTrack Genesis = new() { Id = new SpotifyTrackId("trk-genesis"), Title = "Genesis" };
	private static readonly ProviderTrack Salt = new() { Id = new SpotifyTrackId("trk-salt"), Title = "Salt in the Wounds" };

	private readonly Mock<IPlaylistSource> _source = new();
	private readonly Mock<IPlaylists> _playlists = new();
	private readonly Mock<IResolveProviderTracksService> _resolve = new();
	private readonly Mock<TimeProvider> _time = new();
	private readonly Playlist _playlist = new() { ExternalId = RoadTrip, Name = "Road Trip", SnapshotId = "snap-2", ItemCount = 2 };
	private readonly IReadOnlyList<PlaylistItem> _sourceItems = [new(1, Now.AddDays(-9), Genesis), new(2, Now.AddDays(-8), Salt)];

	public ImportPlaylistServiceTests()
	{
		_playlists.Setup(playlists => playlists.GetPlaylist(_playlist.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_playlist);
		_source.Setup(source => source.GetItems(RoadTrip, It.IsAny<CancellationToken>())).ReturnsAsync(_sourceItems);
		_resolve.Setup(resolve => resolve.Run(It.IsAny<IReadOnlyList<ProviderTrack>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<ProviderTrack> tracks, CancellationToken _) => new ResolveProviderTracksResponse(tracks, [], []));
		_time.Setup(time => time.GetUtcNow()).Returns(Now);
	}

	private ImportPlaylistService Import()
	{
		return new ImportPlaylistService(_source.Object, _playlists.Object, _resolve.Object, _time.Object, NullLogger<ImportPlaylistService>.Instance);
	}

	[Fact]
	public async Task Replaces_the_playlists_items_with_the_sources_and_records_the_marker_they_were_copied_at()
	{
		var response = (await Import().Run(_playlist.Id, onlyWhenChanged: false, CancellationToken.None)).GetRight;

		_playlists.Verify(playlists => playlists.ReplaceItems(_playlist.Id, _sourceItems, It.IsAny<CancellationToken>()), Times.Once);
		_playlists.Verify(playlists => playlists.SavePlaylist(_playlist, It.IsAny<CancellationToken>()), Times.Once);
		Assert.Equal("snap-2", _playlist.ImportedSnapshotId);
		Assert.Equal(Now, _playlist.LastImportedAt);
		Assert.False(_playlist.HasChangedSinceImport);
		Assert.True(response.CopiedItems);
		Assert.Equal(2, response.Items);
	}

	[Fact]
	public async Task Resolves_the_tracks_of_the_copied_items_and_reports_the_outcome()
	{
		var created = new Album { Title = "Immersion", Year = 2010 };
		_resolve.Setup(resolve => resolve.Run(It.IsAny<IReadOnlyList<ProviderTrack>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ResolveProviderTracksResponse([Genesis], [Salt], [created]));

		var response = (await Import().Run(_playlist.Id, onlyWhenChanged: false, CancellationToken.None)).GetRight;

		_resolve.Verify(resolve => resolve.Run(It.Is<IReadOnlyList<ProviderTrack>>(tracks => tracks.SequenceEqual(new[] { Genesis, Salt })), It.IsAny<CancellationToken>()), Times.Once);
		Assert.Equal((1, 1), (response.ResolvedItems, response.UnresolvedItems));
		Assert.Equal([created], response.CreatedAlbums);
		Assert.Same(_playlist, response.Playlist);
	}

	[Fact]
	public async Task Copies_again_when_only_changed_playlists_are_wanted_and_the_marker_differs()
	{
		_playlist.ImportedSnapshotId = "snap-1";
		_playlist.LastImportedAt = Now.AddDays(-1);

		var response = (await Import().Run(_playlist.Id, onlyWhenChanged: true, CancellationToken.None)).GetRight;

		Assert.True(response.CopiedItems);
		_source.Verify(source => source.GetItems(RoadTrip, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Skips_the_copy_for_an_unchanged_playlist_but_still_resolves_the_items_it_holds()
	{
		_playlist.ImportedSnapshotId = "snap-2";
		_playlist.LastImportedAt = Now.AddDays(-1);
		_playlists.Setup(playlists => playlists.GetItems(_playlist.Id, It.IsAny<CancellationToken>())).ReturnsAsync([new PlaylistItem(1, null, Salt)]);

		var response = (await Import().Run(_playlist.Id, onlyWhenChanged: true, CancellationToken.None)).GetRight;

		Assert.False(response.CopiedItems);
		Assert.Equal(1, response.Items);
		Assert.Equal(Now.AddDays(-1), _playlist.LastImportedAt);
		_source.Verify(source => source.GetItems(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
		_playlists.Verify(playlists => playlists.ReplaceItems(It.IsAny<PlaylistId>(), It.IsAny<IReadOnlyList<PlaylistItem>>(), It.IsAny<CancellationToken>()), Times.Never);
		_resolve.Verify(resolve => resolve.Run(It.Is<IReadOnlyList<ProviderTrack>>(tracks => tracks.SequenceEqual(new[] { Salt })), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Copies_an_unchanged_playlist_when_asked_to_regardless()
	{
		_playlist.ImportedSnapshotId = "snap-2";
		_playlist.LastImportedAt = Now.AddDays(-1);

		var response = (await Import().Run(_playlist.Id, onlyWhenChanged: false, CancellationToken.None)).GetRight;

		Assert.True(response.CopiedItems);
	}

	[Fact]
	public async Task Copies_a_playlist_whose_provider_gives_no_marker_every_time()
	{
		_playlist.SnapshotId = null;
		_playlist.LastImportedAt = Now.AddDays(-1);

		var response = (await Import().Run(_playlist.Id, onlyWhenChanged: true, CancellationToken.None)).GetRight;

		Assert.True(response.CopiedItems);
	}

	[Fact]
	public async Task Returns_an_error_when_the_playlist_does_not_exist()
	{
		var result = await Import().Run(PlaylistId.New(), onlyWhenChanged: false, CancellationToken.None);

		Assert.IsType<UnableToFindPlaylistError>(Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Returns_an_error_when_the_playlist_cannot_be_read()
	{
		_playlists.Setup(playlists => playlists.GetPlaylist(_playlist.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Import().Run(_playlist.Id, onlyWhenChanged: false, CancellationToken.None);

		Assert.Equal(new UnableToReadPlaylistError("database is locked"), Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Returns_an_error_and_keeps_the_held_items_when_the_source_cannot_be_read()
	{
		_source.Setup(source => source.GetItems(RoadTrip, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not connected"));

		var result = await Import().Run(_playlist.Id, onlyWhenChanged: false, CancellationToken.None);

		Assert.Equal(new UnableToReadPlaylistItemsFromSourceError("not connected"), Assert.Single(result.GetLeft));
		Assert.Null(_playlist.LastImportedAt);
		_playlists.Verify(playlists => playlists.ReplaceItems(It.IsAny<PlaylistId>(), It.IsAny<IReadOnlyList<PlaylistItem>>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Returns_an_error_when_the_items_cannot_be_saved()
	{
		_playlists.Setup(playlists => playlists.ReplaceItems(_playlist.Id, It.IsAny<IReadOnlyList<PlaylistItem>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("disk full"));

		var result = await Import().Run(_playlist.Id, onlyWhenChanged: false, CancellationToken.None);

		Assert.Equal(new UnableToSavePlaylistItemsError("disk full"), Assert.Single(result.GetLeft));
		_resolve.Verify(resolve => resolve.Run(It.IsAny<IReadOnlyList<ProviderTrack>>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Returns_an_error_when_the_items_cannot_be_resolved()
	{
		_resolve.Setup(resolve => resolve.Run(It.IsAny<IReadOnlyList<ProviderTrack>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<IResolveProviderTracksError> { new UnableToReadCatalogueError("database is locked") });

		var result = await Import().Run(_playlist.Id, onlyWhenChanged: false, CancellationToken.None);

		Assert.IsType<UnableToResolvePlaylistItemsError>(Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_becoming_an_error()
	{
		_source.Setup(source => source.GetItems(RoadTrip, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => Import().Run(_playlist.Id, onlyWhenChanged: false, CancellationToken.None));
	}
}
