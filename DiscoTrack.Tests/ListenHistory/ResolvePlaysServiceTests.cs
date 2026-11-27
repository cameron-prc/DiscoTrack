using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.Discography;
using DiscoTrack.Discography.SyncAlbumWithDiscography;
using DiscoTrack.ExternalIds;
using DiscoTrack.ListenHistory;
using DiscoTrack.ListenHistory.ResolvePlays;
using DiscoTrack.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.ListenHistory;

public class ResolvePlaysServiceTests
{
	private static readonly SpotifyAlbumId SpotifyImmersion = new("sp-immersion");
	private static readonly MusicBrainzReleaseId ImmersionRelease = new("rel-immersion");
	private static readonly Isrc GenesisIsrc = new("GBAHT1000182");

	private readonly Mock<IDiscographySource> _source = new();
	private readonly Mock<ICatalogue> _catalogue = new();
	private readonly Mock<ISyncAlbumWithDiscographyService> _syncAlbum = new();
	private readonly List<Album> _albums = [];
	private readonly List<Artist> _artists = [];
	private readonly Dictionary<AlbumId, IReadOnlyList<Track>> _tracks = [];
	private readonly List<Album> _savedAlbums = [];
	private readonly List<Artist> _savedArtists = [];
	private readonly Artist _pendulum = new() { ExternalIds = [new MusicBrainzArtistId("mb-pendulum")], Name = "Pendulum" };
	private readonly Artist _inFlames = new() { ExternalIds = [new MusicBrainzArtistId("mb-in-flames")], Name = "In Flames" };

	public ResolvePlaysServiceTests()
	{
		_catalogue.Setup(catalogue => catalogue.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync(() => _albums.ToList());
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync(() => _artists.ToList());
		_catalogue.Setup(catalogue => catalogue.GetAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((AlbumId albumId, CancellationToken _) => _tracks.GetValueOrDefault(albumId, []));
		_catalogue.Setup(catalogue => catalogue.SaveAlbum(It.IsAny<Album>(), It.IsAny<CancellationToken>()))
			.Callback((Album album, CancellationToken _) => _savedAlbums.Add(album));
		_catalogue.Setup(catalogue => catalogue.SaveAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()))
			.Callback((AlbumId albumId, IReadOnlyList<Track> tracks, CancellationToken _) => _tracks[albumId] = tracks);
		_catalogue.Setup(catalogue => catalogue.SaveArtist(It.IsAny<Artist>(), It.IsAny<CancellationToken>()))
			.Callback((Artist artist, CancellationToken _) =>
			{
				_savedArtists.Add(artist);

				if (!_artists.Contains(artist))
				{
					_artists.Add(artist);
				}
			});
		_source.Setup(source => source.FindLinkedReleases(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		_source.Setup(source => source.FindRecordings(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		_source.SetupGet(source => source.ReleaseProvider).Returns(Provider.MusicBrainzRelease);
		_source.SetupGet(source => source.ReleaseGroupProvider).Returns(Provider.MusicBrainzReleaseGroup);
		_source.SetupGet(source => source.ArtistProvider).Returns(Provider.MusicBrainzArtist);
		_source.SetupGet(source => source.RecordingProvider).Returns(Provider.MusicBrainzRecording);

		// Stands in for the album sync: the album keeps its ids, takes the discography's title and credits, and gets its tracks
		_syncAlbum.Setup(sync => sync.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((AlbumId albumId, CancellationToken _) =>
			{
				var saved = _savedAlbums.Last(album => album.Id == albumId);
				var synced = new Album
				{
					Id = albumId,
					Title = "Immersion",
					Year = 2010,
					ExternalIds = saved.ExternalIds,
					Artists = [new AlbumArtist { Artist = _pendulum, Position = 1 }],
				};
				var genesis = Track(albumId, 1, 1, "Genesis", _pendulum);
				var selfVsSelf = Track(albumId, 1, 12, "Self vs. Self", _pendulum, _inFlames);

				genesis.ExternalIds = [new MusicBrainzRecordingId("rec-genesis")];
				selfVsSelf.ExternalIds = [new MusicBrainzRecordingId("rec-self")];
				_tracks[albumId] = [genesis, selfVsSelf];

				return new SyncAlbumWithDiscographyResponse(synced, [], [], [], [], null, _tracks[albumId]);
			});
	}

	private ResolvePlaysService Resolve()
	{
		return new ResolvePlaysService(_source.Object, _catalogue.Object, _syncAlbum.Object, NullLogger<ResolvePlaysService>.Instance);
	}

	[Fact]
	public async Task Creates_an_untracked_album_from_the_release_the_discography_links_to_the_plays_album_and_syncs_it()
	{
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);
		var play = Play("sp-genesis", "Genesis", 1, 1);

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		var created = Assert.Single(response.CreatedAlbums);
		Assert.False(created.Tracked);
		Assert.Equal([ImmersionRelease, SpotifyImmersion], created.ExternalIds);
		Assert.Equal("Immersion", created.Title);
		Assert.Equal([play], response.ResolvedPlays);
		Assert.Empty(response.UnresolvedPlays);
		_syncAlbum.Verify(sync => sync.Run(created.Id, It.IsAny<CancellationToken>()), Times.Once);
		_source.Verify(source => source.FindRecordings(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Gives_the_played_track_the_plays_track_id_and_isrc_and_saves_the_albums_tracks()
	{
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		var tracks = _tracks[response.CreatedAlbums[0].Id];
		Assert.Equal([new MusicBrainzRecordingId("rec-genesis"), new SpotifyTrackId("sp-genesis"), GenesisIsrc], tracks[0].ExternalIds);
		Assert.Equal([new MusicBrainzRecordingId("rec-self")], tracks[1].ExternalIds);
		_catalogue.Verify(catalogue => catalogue.SaveAlbumTracks(response.CreatedAlbums[0].Id, tracks, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Gives_each_play_artist_id_to_the_credited_artist_of_the_same_name_and_saves_it()
	{
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);
		var play = Play("sp-self", "Self vs Self", 1, 12) with
		{
			Artists = [new PlayArtist(new SpotifyArtistId("sp-pendulum"), "Pendulum"), new PlayArtist(new SpotifyArtistId("sp-in-flames"), "In Flames")],
		};

		await Resolve().Run([play], CancellationToken.None);

		Assert.Equal([new MusicBrainzArtistId("mb-pendulum"), new SpotifyArtistId("sp-pendulum")], _pendulum.ExternalIds);
		Assert.Equal([new MusicBrainzArtistId("mb-in-flames"), new SpotifyArtistId("sp-in-flames")], _inFlames.ExternalIds);
		Assert.Equal([_pendulum, _inFlames], _savedArtists);
	}

	[Fact]
	public async Task Leaves_an_artist_alone_when_its_id_is_already_held_or_no_credited_artist_shares_its_name()
	{
		_artists.Add(new Artist { ExternalIds = [new SpotifyArtistId("sp-pendulum")], Name = "Pendulum (elsewhere)" });
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);
		var play = Play("sp-genesis", "Genesis", 1, 1) with
		{
			Artists = [new PlayArtist(new SpotifyArtistId("sp-pendulum"), "Pendulum"), new PlayArtist(new SpotifyArtistId("sp-stranger"), "A Stranger")],
		};

		await Resolve().Run([play], CancellationToken.None);

		Assert.Equal([new MusicBrainzArtistId("mb-pendulum")], _pendulum.ExternalIds);
		Assert.Empty(_savedArtists);
	}

	[Fact]
	public async Task Falls_back_to_a_release_of_the_isrcs_recording_whose_title_matches_the_album()
	{
		SetupRecordingReleases(
			new DiscographyRelease(new MusicBrainzReleaseId("rel-single"), "Witchcraft", true, "2010-07-18", 4),
			new DiscographyRelease(ImmersionRelease, "IMMERSION", true, "2010-05-21", 15));

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		Assert.Equal([ImmersionRelease, SpotifyImmersion], Assert.Single(response.CreatedAlbums).ExternalIds);
	}

	[Fact]
	public async Task Prefers_an_official_release_then_the_earliest_then_the_most_common_track_count()
	{
		SetupRecordingReleases(
			new DiscographyRelease(new MusicBrainzReleaseId("rel-promo"), "Immersion", false, "2010-04-01", 15),
			new DiscographyRelease(new MusicBrainzReleaseId("rel-reissue"), "Immersion", true, "2011-01-25", 15),
			new DiscographyRelease(new MusicBrainzReleaseId("rel-japan"), "Immersion", true, "2010-05-21", 17),
			new DiscographyRelease(ImmersionRelease, "Immersion", true, "2010-05-21", 15),
			new DiscographyRelease(new MusicBrainzReleaseId("rel-undated"), "Immersion", true, null, 15));

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		Assert.Contains(ImmersionRelease, Assert.Single(response.CreatedAlbums).ExternalIds);
	}

	[Fact]
	public async Task Accepts_a_release_whose_title_the_albums_edition_title_starts_with()
	{
		SetupRecordingReleases(new DiscographyRelease(ImmersionRelease, "Immersion", true, "2010-05-21", 15));
		var play = Play("sp-genesis", "Genesis", 1, 1) with { AlbumTitle = "Immersion (Deluxe Edition)" };

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		Assert.Single(response.CreatedAlbums);
	}

	[Fact]
	public async Task Guesses_a_release_of_the_isrcs_recording_when_none_carries_the_albums_title()
	{
		SetupRecordingReleases(
			new DiscographyRelease(new MusicBrainzReleaseId("rel-compilation"), "Drum & Bass Arena", true, "2011-03-01", 40),
			new DiscographyRelease(new MusicBrainzReleaseId("rel-single"), "Witchcraft", true, "2010-07-18", 4));

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		Assert.Equal([new MusicBrainzReleaseId("rel-single"), SpotifyImmersion], Assert.Single(response.CreatedAlbums).ExternalIds);
		Assert.Single(response.ResolvedPlays);
	}

	[Fact]
	public async Task Creates_the_album_its_artist_and_the_played_track_from_the_play_when_the_discography_has_no_release()
	{
		SetupRecordingReleases();
		var play = Play("sp-genesis", "Genesis", 1, 1) with { DurationMs = 69000 };

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		Assert.Equal([play], response.ResolvedPlays);
		var album = Assert.Single(response.CreatedAlbums);
		Assert.Equal([SpotifyImmersion], album.ExternalIds);
		Assert.Equal("Immersion", album.Title);
		Assert.False(album.Tracked);
		var artist = Assert.Single(album.Artists).Artist;
		Assert.Equal("Pendulum", artist.Name);
		Assert.Equal([new SpotifyArtistId("sp-pendulum")], artist.ExternalIds);
		Assert.False(artist.Tracked);
		Assert.Equal([artist], _savedArtists);
		Assert.Equal([album], _savedAlbums);
		var track = Assert.Single(_tracks[album.Id]);
		Assert.Equal(("Genesis", 1, 1, (int?)69000), (track.Title, track.DiscNumber, track.Position, track.DurationMs));
		Assert.Equal([new SpotifyTrackId("sp-genesis"), GenesisIsrc], track.ExternalIds);
		Assert.Same(artist, Assert.Single(track.Artists).Artist);
		_syncAlbum.Verify(sync => sync.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Credits_a_created_album_to_the_one_catalogue_artist_of_that_name_and_asks_the_discography_for_the_album_by_that_artist()
	{
		_artists.Add(_pendulum);
		_syncAlbum.Setup(sync => sync.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<ISyncAlbumWithDiscographyError> { new UnableToFindDiscographyAlbumError() });
		var play = Play("sp-genesis", "Genesis", 1, 1);

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		Assert.Equal([play], response.ResolvedPlays);
		var album = Assert.Single(response.CreatedAlbums);
		Assert.Same(_pendulum, Assert.Single(album.Artists).Artist);
		Assert.Equal([new MusicBrainzArtistId("mb-pendulum"), new SpotifyArtistId("sp-pendulum")], _pendulum.ExternalIds);
		Assert.Equal([_pendulum], _savedArtists);
		Assert.Equal("Genesis", Assert.Single(_tracks[album.Id]).Title);
		_syncAlbum.Verify(sync => sync.Run(album.Id, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Uses_the_discography_album_found_by_the_created_albums_artist()
	{
		_artists.Add(_pendulum);

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		var album = Assert.Single(response.CreatedAlbums);
		Assert.Equal(2010, album.Year);
		Assert.Equal(2, _tracks[album.Id].Count);
		Assert.Equal([new MusicBrainzRecordingId("rec-genesis"), new SpotifyTrackId("sp-genesis"), GenesisIsrc], _tracks[album.Id][0].ExternalIds);
	}

	[Fact]
	public async Task Creates_a_new_artist_rather_than_choosing_between_catalogue_artists_of_the_same_name()
	{
		var otherPendulum = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-pendulum-70s")], Name = "Pendulum" };
		_artists.AddRange([_pendulum, otherPendulum]);

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		var artist = Assert.Single(response.CreatedAlbums[0].Artists).Artist;
		Assert.NotSame(_pendulum, artist);
		Assert.NotSame(otherPendulum, artist);
		Assert.Equal([new SpotifyArtistId("sp-pendulum")], artist.ExternalIds);
		Assert.Single(_pendulum.ExternalIds);
		Assert.Single(otherPendulum.ExternalIds);
	}

	[Fact]
	public async Task Credits_a_created_album_to_the_artist_already_holding_the_play_artists_id_without_saving_it()
	{
		_pendulum.SetExternalId(new SpotifyArtistId("sp-pendulum"));
		_artists.Add(_pendulum);
		_syncAlbum.Setup(sync => sync.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<ISyncAlbumWithDiscographyError> { new UnableToFindDiscographyAlbumError() });

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		Assert.Same(_pendulum, Assert.Single(response.CreatedAlbums[0].Artists).Artist);
		Assert.Empty(_savedArtists);
	}

	[Fact]
	public async Task Creates_nothing_when_the_discography_could_not_be_asked()
	{
		_source.Setup(source => source.FindRecordings(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("busy"));
		var play = Play("sp-genesis", "Genesis", 1, 1);

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		Assert.Equal([play], response.UnresolvedPlays);
		Assert.Empty(response.CreatedAlbums);
		Assert.Empty(_savedAlbums);
		Assert.Empty(_savedArtists);
	}

	[Fact]
	public async Task Adds_a_played_track_to_an_album_that_has_no_discography_tracklist()
	{
		var album = new Album { ExternalIds = [SpotifyImmersion], Title = "Immersion", Year = 0 };
		_albums.Add(album);
		var genesis = Track(album.Id, 1, 1, "Genesis", _pendulum);
		genesis.ExternalIds = [new SpotifyTrackId("sp-genesis"), GenesisIsrc];
		_tracks[album.Id] = [genesis];
		var play = Play("sp-self", "Self vs Self", 1, 12) with { Isrc = "GBAHT1000193" };

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		Assert.Equal([play], response.ResolvedPlays);
		Assert.Empty(response.CreatedAlbums);
		Assert.Equal(["Genesis", "Self vs Self"], _tracks[album.Id].Select(track => track.Title));
		Assert.Equal([new SpotifyTrackId("sp-self"), new Isrc("GBAHT1000193")], _tracks[album.Id][1].ExternalIds);
		_source.Verify(source => source.FindLinkedReleases(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
		_syncAlbum.Verify(sync => sync.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Leaves_a_play_without_an_album_unresolved_without_asking_the_discography()
	{
		var play = Play("sp-genesis", "Genesis", 1, 1) with { AlbumId = null };

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		Assert.Equal([play], response.UnresolvedPlays);
		_source.Verify(source => source.FindLinkedReleases(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Reuses_the_catalogue_album_that_already_holds_the_release_and_does_not_report_it_as_created()
	{
		var owned = new Album { ExternalIds = [new SubsonicId("alb-immersion"), ImmersionRelease], Title = "Immersion", Year = 2010, Tracked = true };
		_albums.Add(owned);
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		Assert.Empty(response.CreatedAlbums);
		Assert.Equal([new SubsonicId("alb-immersion"), ImmersionRelease, SpotifyImmersion], Assert.Single(_savedAlbums).ExternalIds);
		Assert.Same(owned, _savedAlbums[0]);
		Assert.Single(response.ResolvedPlays);
	}

	[Fact]
	public async Task Resolves_a_play_of_an_album_already_holding_the_provider_id_without_asking_the_discography()
	{
		var album = new Album { ExternalIds = [ImmersionRelease, SpotifyImmersion], Title = "Immersion", Year = 2010 };
		_albums.Add(album);
		_tracks[album.Id] = [Track(album.Id, 1, 1, "Genesis", _pendulum)];

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		Assert.Single(response.ResolvedPlays);
		Assert.Equal([new SpotifyTrackId("sp-genesis"), GenesisIsrc], _tracks[album.Id][0].ExternalIds);
		_source.Verify(source => source.FindLinkedReleases(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
		_syncAlbum.Verify(sync => sync.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Saves_nothing_for_a_play_whose_track_already_holds_its_id()
	{
		var album = new Album { ExternalIds = [ImmersionRelease, SpotifyImmersion], Title = "Immersion", Year = 2010 };
		_albums.Add(album);
		var genesis = Track(album.Id, 1, 1, "Genesis", _pendulum);
		genesis.ExternalIds = [new SpotifyTrackId("sp-genesis"), GenesisIsrc];
		_tracks[album.Id] = [genesis];

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		Assert.Single(response.ResolvedPlays);
		_catalogue.Verify(catalogue => catalogue.SaveAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Syncs_an_album_that_holds_the_provider_id_but_has_no_tracks_before_matching()
	{
		var album = new Album { ExternalIds = [ImmersionRelease, SpotifyImmersion], Title = "Immersion", Year = 0 };
		_albums.Add(album);
		_savedAlbums.Add(album);

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None)).GetRight;

		Assert.Single(response.ResolvedPlays);
		_syncAlbum.Verify(sync => sync.Run(album.Id, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Theory]
	[InlineData("Genesis", 1, 1, "Genesis")]
	[InlineData("genesis!", 2, 9, "Genesis")]
	[InlineData("Self vs Self - Remastered 2020", 1, 12, "Self vs. Self")]
	public async Task Matches_a_track_by_place_and_title_by_title_alone_or_by_place_with_an_edition_suffix(string playTitle, int disc, int number, string expectedTrack)
	{
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);

		var response = (await Resolve().Run([Play("sp-track", playTitle, disc, number)], CancellationToken.None)).GetRight;

		var tagged = _tracks[response.CreatedAlbums[0].Id].Single(track => track.ExternalIds.Contains(new SpotifyTrackId("sp-track")));
		Assert.Equal(expectedTrack, tagged.Title);
	}

	[Fact]
	public async Task Matches_a_track_holding_the_plays_isrc_whatever_its_title_and_place()
	{
		var album = new Album { ExternalIds = [ImmersionRelease, SpotifyImmersion], Title = "Immersion", Year = 2010 };
		_albums.Add(album);
		var genesis = Track(album.Id, 1, 1, "Genesis", _pendulum);
		var selfVsSelf = Track(album.Id, 1, 12, "Self vs. Self", _pendulum);
		selfVsSelf.ExternalIds = [new MusicBrainzRecordingId("rec-self"), new Isrc("gbaht1000182")];
		_tracks[album.Id] = [genesis, selfVsSelf];

		var response = (await Resolve().Run([Play("sp-track", "Genesis [Feat. Nobody] - 2020 Remaster", 3, 7)], CancellationToken.None)).GetRight;

		Assert.Single(response.ResolvedPlays);
		Assert.Equal([new MusicBrainzRecordingId("rec-self"), GenesisIsrc, new SpotifyTrackId("sp-track")], selfVsSelf.ExternalIds);
		Assert.Empty(genesis.ExternalIds);
	}

	[Fact]
	public async Task Prefers_the_track_holding_the_plays_track_id_over_one_holding_its_isrc()
	{
		var album = new Album { ExternalIds = [ImmersionRelease, SpotifyImmersion], Title = "Immersion", Year = 2010 };
		_albums.Add(album);
		var sameIsrc = Track(album.Id, 1, 1, "Genesis", _pendulum);
		sameIsrc.ExternalIds = [GenesisIsrc];
		var sameTrack = Track(album.Id, 2, 1, "Genesis (reprise)", _pendulum);
		sameTrack.ExternalIds = [new SpotifyTrackId("sp-genesis")];
		_tracks[album.Id] = [sameIsrc, sameTrack];

		await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None);

		Assert.Equal([new SpotifyTrackId("sp-genesis"), GenesisIsrc], sameTrack.ExternalIds);
		Assert.Equal([GenesisIsrc], sameIsrc.ExternalIds);
	}

	[Fact]
	public async Task Leaves_a_play_unresolved_when_the_album_has_no_matching_track()
	{
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);
		var play = Play("sp-bonus", "Bonus Track", 1, 16);

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		Assert.Equal([play], response.UnresolvedPlays);
		Assert.Single(response.CreatedAlbums);
	}

	[Fact]
	public async Task Resolves_the_album_once_for_several_plays_of_it()
	{
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);

		var response = (await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1), Play("sp-self", "Self vs Self", 1, 12)], CancellationToken.None)).GetRight;

		Assert.Equal(2, response.ResolvedPlays.Count);
		Assert.Single(response.CreatedAlbums);
		_source.Verify(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Leaves_the_plays_of_an_album_unresolved_when_its_sync_fails_and_carries_on_with_the_next_album()
	{
		var otherAlbum = new SpotifyAlbumId("sp-other");
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);
		_source.Setup(source => source.FindLinkedReleases(otherAlbum, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("rate limited"));
		var failed = Play("sp-x", "X", 1, 1) with { AlbumId = otherAlbum };
		var resolved = Play("sp-genesis", "Genesis", 1, 1);

		var response = (await Resolve().Run([failed, resolved], CancellationToken.None)).GetRight;

		Assert.Equal([resolved], response.ResolvedPlays);
		Assert.Equal([failed], response.UnresolvedPlays);
	}

	[Fact]
	public async Task Leaves_plays_unresolved_when_the_album_sync_returns_errors()
	{
		_source.Setup(source => source.FindLinkedReleases(SpotifyImmersion, It.IsAny<CancellationToken>())).ReturnsAsync([ImmersionRelease]);
		_syncAlbum.Setup(sync => sync.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new List<ISyncAlbumWithDiscographyError> { new UnableToFindDiscographyAlbumError() });
		var play = Play("sp-genesis", "Genesis", 1, 1);

		var response = (await Resolve().Run([play], CancellationToken.None)).GetRight;

		Assert.Equal([play], response.UnresolvedPlays);
		Assert.Empty(response.CreatedAlbums);
	}

	[Fact]
	public async Task Returns_an_error_when_the_catalogue_cannot_be_read()
	{
		_catalogue.Setup(catalogue => catalogue.GetAlbums(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None);

		Assert.Equal(new UnableToReadCatalogueError("database is locked"), Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_leaving_plays_unresolved()
	{
		_source.Setup(source => source.FindLinkedReleases(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => Resolve().Run([Play("sp-genesis", "Genesis", 1, 1)], CancellationToken.None));
	}

	private void SetupRecordingReleases(params DiscographyRelease[] releases)
	{
		_source.Setup(source => source.FindRecordings("GBAHT1000182", It.IsAny<CancellationToken>()))
			.ReturnsAsync([new DiscographyRecording(new MusicBrainzRecordingId("rec-genesis"), releases)]);
	}

	private static Play Play(string trackId, string title, int disc, int number)
	{
		return new Play
		{
			PlayedAt = new DateTimeOffset(2026, 11, 27, 8, 0, 0, TimeSpan.Zero),
			TrackId = new SpotifyTrackId(trackId),
			TrackTitle = title,
			Isrc = "GBAHT1000182",
			DiscNumber = disc,
			TrackNumber = number,
			Artists = [new PlayArtist(new SpotifyArtistId("sp-pendulum"), "Pendulum")],
			AlbumId = SpotifyImmersion,
			AlbumTitle = "Immersion",
		};
	}

	private static Track Track(AlbumId albumId, int disc, int position, string title, params Artist[] artists)
	{
		return new Track
		{
			AlbumId = albumId,
			Title = title,
			DiscNumber = disc,
			Position = position,
			Artists = artists.Select((artist, index) => new TrackArtist { Artist = artist, Position = index + 1 }).ToList(),
		};
	}
}
