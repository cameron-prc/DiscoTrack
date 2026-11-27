using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Clients.Spotify.Models;
using DiscoTrack.Infrastructure.Playlists;
using DiscoTrack.Playlists;
using Moq;
using Playlist = DiscoTrack.Infrastructure.Clients.Spotify.Models.Playlist;
using PlaylistItem = DiscoTrack.Infrastructure.Clients.Spotify.Models.PlaylistItem;

namespace DiscoTrack.Infrastructure.Tests.Playlists;

public class SpotifyPlaylistSourceTests
{
	private static readonly DateTimeOffset AddedAt = new(2026, 7, 6, 7, 10, 5, TimeSpan.Zero);

	private readonly Mock<ISpotifyClient> _client = new();

	private SpotifyPlaylistSource Source()
	{
		return new SpotifyPlaylistSource(_client.Object);
	}

	[Fact]
	public void PlaylistProvider_is_the_spotify_playlist_provider()
	{
		Assert.Equal(Provider.SpotifyPlaylist, Source().PlaylistProvider);
	}




	[Theory]
	[InlineData("https://open.spotify.com/playlist/3cEYpjA9oz9GiPac4AsH4n")]
	[InlineData("https://open.spotify.com/playlist/3cEYpjA9oz9GiPac4AsH4n?si=0123456789abcdef")]
	[InlineData("http://open.spotify.com/playlist/3cEYpjA9oz9GiPac4AsH4n/")]
	[InlineData("https://open.spotify.com/intl-de/playlist/3cEYpjA9oz9GiPac4AsH4n")]
	[InlineData("spotify:playlist:3cEYpjA9oz9GiPac4AsH4n")]
	[InlineData("3cEYpjA9oz9GiPac4AsH4n")]
	[InlineData("  3cEYpjA9oz9GiPac4AsH4n  ")]
	public void FindPlaylistId_reads_the_id_from_a_link_a_uri_or_the_id_alone(string reference)
	{
		Assert.Equal(new SpotifyPlaylistId("3cEYpjA9oz9GiPac4AsH4n"), Source().FindPlaylistId(reference));
	}

	[Theory]
	[InlineData("")]
	[InlineData("Road Trip")]
	[InlineData("https://open.spotify.com/album/3cEYpjA9oz9GiPac4AsH4n")]
	[InlineData("https://example.com/playlist/3cEYpjA9oz9GiPac4AsH4n")]
	[InlineData("spotify:track:3cEYpjA9oz9GiPac4AsH4n")]
	[InlineData("3cEYpjA9oz9GiPac4AsH4")]
	[InlineData("3cEYpjA9oz9GiPac4AsH4n3cEYpjA9oz9GiPac4AsH4n")]
	public void FindPlaylistId_finds_nothing_in_anything_else(string reference)
	{
		Assert.Null(Source().FindPlaylistId(reference));
	}

	[Fact]
	public async Task GetPlaylist_maps_what_describes_the_playlist()
	{
		_client.Setup(client => client.GetPlaylist("pl-road-trip", It.IsAny<CancellationToken>())).ReturnsAsync(new Playlist
		{
			Id = "pl-road-trip", Name = "Road Trip", Description = "", SnapshotId = "snap-1",
			Owner = new PlaylistOwner { Id = "friend" },
			Tracks = new PlaylistTracks { Total = 42 },
		});

		var playlist = await Source().GetPlaylist(new SpotifyPlaylistId("pl-road-trip"), CancellationToken.None);

		Assert.Equal(new ProviderPlaylist(new SpotifyPlaylistId("pl-road-trip"), "Road Trip", null, "friend", "snap-1", 42), playlist);
	}

	[Fact]
	public async Task GetPlaylist_returns_null_when_spotify_has_no_such_playlist_and_rejects_another_providers_id()
	{
		_client.Setup(client => client.GetPlaylist("pl-gone", It.IsAny<CancellationToken>())).ReturnsAsync((Playlist?)null);

		Assert.Null(await Source().GetPlaylist(new SpotifyPlaylistId("pl-gone"), CancellationToken.None));
		await Assert.ThrowsAsync<ArgumentException>(() => Source().GetPlaylist(new SpotifyAlbumId("alb-immersion"), CancellationToken.None));
	}

	[Fact]
	public async Task GetItems_maps_each_song_in_order_with_when_it_was_added()
	{
		SetupItemPage("pl-road-trip", 0, next: false, Song("trk-self", "Self vs Self"), Song("trk-genesis", "Genesis"));

		var items = await Source().GetItems(new SpotifyPlaylistId("pl-road-trip"), CancellationToken.None);

		Assert.Equal([1, 2], items.Select(item => item.Position));
		Assert.Equal(AddedAt, items[0].AddedAt);
		var track = items[0].Track;
		Assert.Equal(new SpotifyTrackId("trk-self"), track.Id);
		Assert.Equal("Self vs Self", track.Title);
		Assert.Equal("GBAHT1000190", track.Isrc);
		Assert.Equal((1, 12, (int?)285000), (track.DiscNumber, track.TrackNumber, track.DurationMs));
		Assert.Equal(new SpotifyAlbumId("alb-immersion"), track.AlbumId);
		Assert.Equal("Immersion", track.AlbumTitle);
		Assert.Equal("Pendulum", Assert.Single(track.Artists).Name);
	}

	[Fact]
	public async Task GetItems_leaves_out_anything_that_is_not_a_song_and_numbers_the_songs_without_gaps()
	{
		SetupItemPage(
			"pl-road-trip",
			0,
			next: false,
			Song("trk-self", "Self vs Self"),
			new PlaylistItem { Track = new Track { Id = "ep-12", Name = "Episode 12", Type = "episode" } },
			new PlaylistItem { IsLocal = true, Track = new Track { Id = null, Name = "Live in a Field", Type = "track" } },
			new PlaylistItem { Track = null },
			null,
			Song("trk-genesis", "Genesis"));

		var items = await Source().GetItems(new SpotifyPlaylistId("pl-road-trip"), CancellationToken.None);

		Assert.Equal([(1, "Self vs Self"), (2, "Genesis")], items.Select(item => (item.Position, item.Track.Title)));
	}

	[Fact]
	public async Task GetItems_reads_every_page()
	{
		SetupItemPage("pl-road-trip", 0, next: true, Song("trk-1", "One"), new PlaylistItem { Track = null });
		SetupItemPage("pl-road-trip", 2, next: false, Song("trk-2", "Two"));

		var items = await Source().GetItems(new SpotifyPlaylistId("pl-road-trip"), CancellationToken.None);

		Assert.Equal([(1, "One"), (2, "Two")], items.Select(item => (item.Position, item.Track.Title)));
		_client.Verify(client => client.GetPlaylistItems("pl-road-trip", 100, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
	}

	[Fact]
	public async Task GetItems_rejects_an_id_that_is_not_a_spotify_playlist()
	{
		await Assert.ThrowsAsync<ArgumentException>(() => Source().GetItems(new SpotifyAlbumId("alb-immersion"), CancellationToken.None));
	}


	private void SetupItemPage(string playlistId, int offset, bool next, params PlaylistItem?[] items)
	{
		_client.Setup(client => client.GetPlaylistItems(playlistId, It.IsAny<int>(), offset, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new Page<PlaylistItem> { Items = items, Next = next ? "next" : null });
	}

	private static PlaylistItem Song(string id, string name)
	{
		return new PlaylistItem
		{
			AddedAt = AddedAt,
			Track = new Track
			{
				Id = id, Name = name, Type = "track", DurationMs = 285000, DiscNumber = 1, TrackNumber = 12,
				ExternalIds = new TrackExternalIds { Isrc = "GBAHT1000190" },
				Artists = [new Artist { Id = "art-pendulum", Name = "Pendulum" }],
				Album = new Album { Id = "alb-immersion", Name = "Immersion" },
			},
		};
	}
}
