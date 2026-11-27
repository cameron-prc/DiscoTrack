using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Playlists;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Sqlite.Migrations;
using DiscoTrack.Infrastructure.Tests.Sqlite;
using DiscoTrack.Playlists;
using DiscoTrack.Providers;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tests.Playlists;

public sealed class SqlitePlaylistsTests : IDisposable
{
	private static readonly DateTimeOffset Morning = new(2026, 11, 27, 8, 0, 0, 123, TimeSpan.Zero);

	private readonly TestDatabase _db = new();
	private readonly SqlitePlaylists _playlists;

	public SqlitePlaylistsTests()
	{
		Migrator.Migrate(_db.ConnectionString);
		_playlists = new SqlitePlaylists(Options.Create(new SqliteOptions { ConnectionString = _db.ConnectionString }));
	}

	public void Dispose()
	{
		_db.Dispose();
	}

	[Fact]
	public async Task A_fresh_store_holds_no_playlists()
	{
		Assert.Empty(await _playlists.GetPlaylists(CancellationToken.None));
		Assert.Null(await _playlists.GetPlaylist(PlaylistId.New(), CancellationToken.None));
		Assert.Empty(await _playlists.GetItems(PlaylistId.New(), CancellationToken.None));
	}

	[Fact]
	public async Task SavePlaylist_round_trips_every_field()
	{
		var playlist = new Playlist
		{
			ExternalId = new SpotifyPlaylistId("pl-road-trip"),
			Name = "Road Trip",
			Description = "For the drive north",
			OwnerName = "Example Listener",
			SnapshotId = "snap-2",
			ItemCount = 42,
			Tracked = true,
			ImportedSnapshotId = "snap-1",
			LastImportedAt = Morning,
			RemovedAt = Morning.AddDays(1),
		};

		await _playlists.SavePlaylist(playlist, CancellationToken.None);

		var stored = await _playlists.GetPlaylist(playlist.Id, CancellationToken.None);
		Assert.NotNull(stored);
		Assert.Equal(playlist.Id, stored.Id);
		Assert.Equal(new SpotifyPlaylistId("pl-road-trip"), stored.ExternalId);
		Assert.Equal(("Road Trip", "For the drive north", "Example Listener", "snap-2", 42), (stored.Name, stored.Description, stored.OwnerName, stored.SnapshotId, stored.ItemCount));
		Assert.True(stored.Tracked);
		Assert.Equal("snap-1", stored.ImportedSnapshotId);
		Assert.Equal(Morning, stored.LastImportedAt);
		Assert.Equal(Morning.AddDays(1), stored.RemovedAt);
	}

	[Fact]
	public async Task SavePlaylist_round_trips_a_playlist_with_nothing_optional()
	{
		var playlist = Playlist("pl-bare", "Bare");

		await _playlists.SavePlaylist(playlist, CancellationToken.None);

		var stored = Assert.Single(await _playlists.GetPlaylists(CancellationToken.None));
		Assert.Null(stored.Description);
		Assert.Null(stored.OwnerName);
		Assert.Null(stored.SnapshotId);
		Assert.False(stored.Tracked);
		Assert.Null(stored.ImportedSnapshotId);
		Assert.Null(stored.LastImportedAt);
		Assert.Null(stored.RemovedAt);
	}

	[Fact]
	public async Task SavePlaylist_updates_a_playlist_already_held()
	{
		var playlist = Playlist("pl-road-trip", "Road Trip");
		await _playlists.SavePlaylist(playlist, CancellationToken.None);
		playlist.Name = "Road Trip 2026";
		playlist.Tracked = true;

		await _playlists.SavePlaylist(playlist, CancellationToken.None);

		var stored = Assert.Single(await _playlists.GetPlaylists(CancellationToken.None));
		Assert.Equal("Road Trip 2026", stored.Name);
		Assert.True(stored.Tracked);
	}

	[Fact]
	public async Task GetPlaylists_orders_by_name_whatever_the_letter_case()
	{
		await _playlists.SavePlaylist(Playlist("pl-1", "road trip"), CancellationToken.None);
		await _playlists.SavePlaylist(Playlist("pl-2", "Gym"), CancellationToken.None);
		await _playlists.SavePlaylist(Playlist("pl-3", "Zen"), CancellationToken.None);

		var names = (await _playlists.GetPlaylists(CancellationToken.None)).Select(playlist => playlist.Name);

		Assert.Equal(["Gym", "road trip", "Zen"], names);
	}

	[Fact]
	public async Task ReplaceItems_round_trips_items_in_order_with_their_artists()
	{
		var playlist = Playlist("pl-road-trip", "Road Trip");
		await _playlists.SavePlaylist(playlist, CancellationToken.None);
		var item = new PlaylistItem(1, Morning, new ProviderTrack
		{
			Id = new SpotifyTrackId("trk-self"),
			Title = "Self vs Self",
			Isrc = "GBAHT1000190",
			DurationMs = 285000,
			DiscNumber = 1,
			TrackNumber = 12,
			Artists = [new ProviderArtist(new SpotifyArtistId("art-pendulum"), "Pendulum"), new ProviderArtist(new SpotifyArtistId("art-in-flames"), "In Flames")],
			AlbumId = new SpotifyAlbumId("alb-immersion"),
			AlbumTitle = "Immersion",
		});

		await _playlists.ReplaceItems(playlist.Id, [item, Item(2, "trk-bare")], CancellationToken.None);

		var stored = await _playlists.GetItems(playlist.Id, CancellationToken.None);
		Assert.Equal([1, 2], stored.Select(storedItem => storedItem.Position));
		Assert.Equal(Morning, stored[0].AddedAt);
		var track = stored[0].Track;
		Assert.Equal(new SpotifyTrackId("trk-self"), track.Id);
		Assert.Equal(("Self vs Self", "GBAHT1000190", (int?)285000, (int?)1, (int?)12), (track.Title, track.Isrc, track.DurationMs, track.DiscNumber, track.TrackNumber));
		Assert.Equal(item.Track.Artists, track.Artists);
		Assert.Equal(new SpotifyAlbumId("alb-immersion"), track.AlbumId);
		Assert.Equal("Immersion", track.AlbumTitle);
		Assert.Null(stored[1].AddedAt);
		Assert.Null(stored[1].Track.AlbumId);
		Assert.Empty(stored[1].Track.Artists);
	}

	[Fact]
	public async Task ReplaceItems_leaves_only_the_given_items()
	{
		var playlist = Playlist("pl-road-trip", "Road Trip");
		await _playlists.SavePlaylist(playlist, CancellationToken.None);
		await _playlists.ReplaceItems(playlist.Id, [Item(1, "trk-1", "art-1"), Item(2, "trk-2", "art-2"), Item(3, "trk-3", "art-3")], CancellationToken.None);

		await _playlists.ReplaceItems(playlist.Id, [Item(1, "trk-3", "art-3")], CancellationToken.None);

		var item = Assert.Single(await _playlists.GetItems(playlist.Id, CancellationToken.None));
		Assert.Equal(new SpotifyTrackId("trk-3"), item.Track.Id);
		Assert.Equal(new SpotifyArtistId("art-3"), Assert.Single(item.Track.Artists).Id);
	}

	[Fact]
	public async Task ReplaceItems_keeps_the_same_track_listed_twice()
	{
		var playlist = Playlist("pl-road-trip", "Road Trip");
		await _playlists.SavePlaylist(playlist, CancellationToken.None);

		await _playlists.ReplaceItems(playlist.Id, [Item(1, "trk-1"), Item(2, "trk-1")], CancellationToken.None);

		Assert.Equal(2, (await _playlists.GetItems(playlist.Id, CancellationToken.None)).Count);
	}

	[Fact]
	public async Task ReplaceItems_does_not_touch_another_playlists_items()
	{
		var roadTrip = Playlist("pl-road-trip", "Road Trip");
		var gym = Playlist("pl-gym", "Gym");
		await _playlists.SavePlaylist(roadTrip, CancellationToken.None);
		await _playlists.SavePlaylist(gym, CancellationToken.None);
		await _playlists.ReplaceItems(gym.Id, [Item(1, "trk-gym", "art-1")], CancellationToken.None);

		await _playlists.ReplaceItems(roadTrip.Id, [], CancellationToken.None);

		Assert.Single((await _playlists.GetItems(gym.Id, CancellationToken.None))[0].Track.Artists);
	}

	private static Playlist Playlist(string id, string name)
	{
		return new Playlist { ExternalId = new SpotifyPlaylistId(id), Name = name };
	}

	private static PlaylistItem Item(int position, string trackId, string? artistId = null)
	{
		return new PlaylistItem(position, null, new ProviderTrack
		{
			Id = new SpotifyTrackId(trackId),
			Title = trackId,
			Artists = artistId is null ? [] : [new ProviderArtist(new SpotifyArtistId(artistId), artistId)],
		});
	}
}
