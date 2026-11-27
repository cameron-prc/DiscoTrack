using DiscoTrack.ExternalIds;
using DiscoTrack.Playlists;
using DiscoTrack.Playlists.AddPlaylist;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Playlists;

public class AddPlaylistServiceTests
{
	private const string Link = "https://open.spotify.com/playlist/pl-road-trip";
	private static readonly SpotifyPlaylistId RoadTrip = new("pl-road-trip");

	private readonly Mock<IPlaylistSource> _source = new();
	private readonly Mock<IPlaylists> _playlists = new();
	private readonly List<Playlist> _known = [];
	private readonly List<Playlist> _saved = [];

	public AddPlaylistServiceTests()
	{
		_source.Setup(source => source.FindPlaylistId(Link)).Returns(RoadTrip);
		_source.Setup(source => source.GetPlaylist(RoadTrip, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ProviderPlaylist(RoadTrip, "Road Trip", "For the drive north", "friend", "snap-2", 42));
		_playlists.Setup(playlists => playlists.GetPlaylists(It.IsAny<CancellationToken>())).ReturnsAsync(() => _known.ToList());
		_playlists.Setup(playlists => playlists.SavePlaylist(It.IsAny<Playlist>(), It.IsAny<CancellationToken>()))
			.Callback((Playlist playlist, CancellationToken _) => _saved.Add(playlist));
	}

	private AddPlaylistService Add()
	{
		return new AddPlaylistService(_source.Object, _playlists.Object, NullLogger<AddPlaylistService>.Instance);
	}

	[Fact]
	public async Task Adds_the_playlist_the_link_refers_to_as_tracked_with_what_describes_it()
	{
		var response = (await Add().Run(Link, CancellationToken.None)).GetRight;

		var playlist = Assert.Single(_saved);
		Assert.Same(playlist, response.Playlist);
		Assert.Equal(RoadTrip, playlist.ExternalId);
		Assert.Equal(("Road Trip", "For the drive north", "friend", "snap-2", 42), (playlist.Name, playlist.Description, playlist.OwnerName, playlist.SnapshotId, playlist.ItemCount));
		Assert.True(playlist.Tracked);
		Assert.Null(playlist.LastImportedAt);
		Assert.False(response.AlreadyTracked);
	}

	[Fact]
	public async Task Tracks_and_updates_the_playlist_already_held_rather_than_adding_another()
	{
		var known = new Playlist { ExternalId = RoadTrip, Name = "Road Trip (old name)", SnapshotId = "snap-1", ImportedSnapshotId = "snap-1", RemovedAt = DateTimeOffset.UnixEpoch };
		_known.Add(known);

		var response = (await Add().Run(Link, CancellationToken.None)).GetRight;

		Assert.Same(known, Assert.Single(_saved));
		Assert.True(known.Tracked);
		Assert.Equal(("Road Trip", "snap-2"), (known.Name, known.SnapshotId));
		Assert.Equal("snap-1", known.ImportedSnapshotId);
		Assert.Null(known.RemovedAt);
		Assert.False(response.AlreadyTracked);
	}

	[Fact]
	public async Task Reports_a_playlist_that_was_already_tracked()
	{
		_known.Add(new Playlist { ExternalId = RoadTrip, Name = "Road Trip", Tracked = true });

		var response = (await Add().Run(Link, CancellationToken.None)).GetRight;

		Assert.True(response.AlreadyTracked);
	}

	[Fact]
	public async Task Returns_an_error_without_asking_the_source_when_the_text_is_not_a_playlist_link()
	{
		var result = await Add().Run("Road Trip", CancellationToken.None);

		Assert.IsType<UnrecognisedPlaylistReferenceError>(Assert.Single(result.GetLeft));
		_source.Verify(source => source.GetPlaylist(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Returns_an_error_and_saves_nothing_when_the_source_has_no_such_playlist()
	{
		_source.Setup(source => source.GetPlaylist(RoadTrip, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderPlaylist?)null);

		var result = await Add().Run(Link, CancellationToken.None);

		Assert.IsType<UnableToFindPlaylistAtSourceError>(Assert.Single(result.GetLeft));
		Assert.Empty(_saved);
	}

	[Fact]
	public async Task Returns_an_error_when_the_source_cannot_be_read()
	{
		_source.Setup(source => source.GetPlaylist(RoadTrip, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("not connected"));

		var result = await Add().Run(Link, CancellationToken.None);

		Assert.Equal(new UnableToReadPlaylistSourceError("not connected"), Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Returns_an_error_when_the_playlists_cannot_be_read_or_saved()
	{
		_playlists.Setup(playlists => playlists.SavePlaylist(It.IsAny<Playlist>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("disk full"));

		var saveResult = await Add().Run(Link, CancellationToken.None);

		_playlists.Setup(playlists => playlists.GetPlaylists(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		var readResult = await Add().Run(Link, CancellationToken.None);

		Assert.Equal(new UnableToSavePlaylistError("disk full"), Assert.Single(saveResult.GetLeft));
		Assert.Equal(new UnableToReadPlaylistsError("database is locked"), Assert.Single(readResult.GetLeft));
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_becoming_an_error()
	{
		_source.Setup(source => source.GetPlaylist(RoadTrip, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => Add().Run(Link, CancellationToken.None));
	}
}
