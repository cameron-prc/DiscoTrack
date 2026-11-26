using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;
using DiscoTrack.Infrastructure.Discography;
using DiscoTrack.Infrastructure.Tests.Clients.MusicBrainz;
using Moq;

namespace DiscoTrack.Infrastructure.Tests.Discography;

public class MusicBrainzDiscographySourceTests
{
	private static readonly Artist Tool = new() { Id = "mb-tool", Name = "Tool", Disambiguation = "American rock band" };
	private static readonly Artist Puscifer = new() { Id = "mb-puscifer", Name = "Puscifer", Disambiguation = "" };

	private static readonly ReleaseGroup Lateralus = new()
	{
		Id = "rg-lateralus",
		Title = "Lateralus",
		FirstReleaseDate = "2001-05-14",
		ArtistCredit = [new ArtistCredit { Name = "Tool", Artist = Tool }],
	};

	private readonly Mock<IMusicBrainzClient> _client = new();

	private MusicBrainzDiscographySource Source(int pageSize = 100, int searchSize = 25, int maxReleaseGroupPages = 10)
	{
		return new(_client.Object, TestMusicBrainzOptions.Create(pageSize, searchSize, maxReleaseGroupPages));
	}

	[Fact]
	public void VariousArtistsId_is_the_MusicBrainz_various_artists_artist()
	{
		Assert.Equal(new MusicBrainzArtistId("89ad4ac3-39f7-470e-963a-56509c546377"), Source().VariousArtistsId);
	}

	[Fact]
	public async Task GetAlbum_by_release_returns_its_release_group()
	{
		_client.Setup(client => client.GetRelease("rel-lateralus-cd", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Release("rel-lateralus-cd") with { ReleaseGroup = Lateralus });

		var album = await Source().GetAlbum(new MusicBrainzReleaseId("rel-lateralus-cd"), CancellationToken.None);

		Assert.NotNull(album);
		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus"), album.ReleaseGroupId);
		Assert.Equal("Lateralus", album.Title);
		Assert.Equal(2001, album.Year);
		Assert.False(album.IsCompilation);
		Assert.Equal([new DiscographyArtist(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band")], album.Artists);
	}

	[Fact]
	public async Task GetAlbum_by_release_returns_the_releases_tracks_across_its_discs()
	{
		_client.Setup(client => client.GetRelease("rel-lateralus-cd", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Release("rel-lateralus-cd") with
			{
				ReleaseGroup = Lateralus,
				Media =
				[
					Medium(1, Track(1, "The Grudge", "rec-grudge", 516000, Tool), Track(2, "Schism", "rec-schism", null, Tool, Puscifer) with { Recording = new Recording { Id = "rec-schism", Length = 407000, ArtistCredit = [Credit(Tool), Credit(Puscifer)] } }),
					Medium(2, Track(1, "Bonus", "rec-bonus", null)),
				],
			});

		var album = await Source().GetAlbum(new MusicBrainzReleaseId("rel-lateralus-cd"), CancellationToken.None);

		Assert.Equal(
			[
				(1, 1, "The Grudge", (ExternalId)new MusicBrainzRecordingId("rec-grudge"), (int?)516000),
				(1, 2, "Schism", new MusicBrainzRecordingId("rec-schism"), 407000),
				(2, 1, "Bonus", new MusicBrainzRecordingId("rec-bonus"), null),
			],
			album!.Tracks!.Select(track => (track.DiscNumber, track.Position, track.Title, track.RecordingId, track.LengthMs)));
		Assert.Equal(
			[new DiscographyArtist(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band"), new DiscographyArtist(new MusicBrainzArtistId("mb-puscifer"), "Puscifer", null)],
			album.Tracks![1].Artists);
		Assert.Empty(album.Tracks[2].Artists);
	}

	[Fact]
	public async Task GetAlbum_by_release_group_returns_no_tracks()
	{
		_client.Setup(client => client.GetReleaseGroup("rg-lateralus", It.IsAny<CancellationToken>())).ReturnsAsync(Lateralus);

		var album = await Source().GetAlbum(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.Null(album!.Tracks);
	}

	[Fact]
	public async Task GetAlbumTracks_by_release_returns_its_tracks_and_nothing_when_it_is_not_found()
	{
		_client.Setup(client => client.GetRelease("rel-lateralus-cd", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Release("rel-lateralus-cd") with { Media = [Medium(1, Track(1, "The Grudge", "rec-grudge", 516000, Tool))] });
		_client.Setup(client => client.GetRelease("rel-missing", It.IsAny<CancellationToken>())).ReturnsAsync((Release?)null);

		var tracks = await Source().GetAlbumTracks(new MusicBrainzReleaseId("rel-lateralus-cd"), CancellationToken.None);
		var missing = await Source().GetAlbumTracks(new MusicBrainzReleaseId("rel-missing"), CancellationToken.None);

		Assert.Equal("The Grudge", Assert.Single(tracks).Title);
		Assert.Empty(missing);
	}

	[Fact]
	public async Task GetAlbumTracks_by_release_group_takes_the_earliest_official_release()
	{
		SetupReleases(
			Browsed("rel-bootleg", "Bootleg", "2001-01-01", 13),
			Browsed("rel-reissue", "Official", "2019-08-02", 13),
			Browsed("rel-first", "Official", "2001-05-14", 13),
			Browsed("rel-undated", "Official", null, 13));

		var tracks = await Source().GetAlbumTracks(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.Equal("From rel-first", Assert.Single(tracks).Title);
		_client.Verify(client => client.BrowseReleases("rg-lateralus", 100, 0, It.IsAny<CancellationToken>()), Times.Once);
		_client.Verify(client => client.GetRelease(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task GetAlbumTracks_by_release_group_splits_releases_of_the_same_date_by_the_most_common_track_count()
	{
		SetupReleases(
			Browsed("rel-a-japan", "Official", "2001-05-14", 15),
			Browsed("rel-b-europe", "Official", "2001-05-14", 13),
			Browsed("rel-c-usa", "Official", "2001-05-14", 13));

		var tracks = await Source().GetAlbumTracks(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.Equal("From rel-b-europe", Assert.Single(tracks).Title);
	}

	[Fact]
	public async Task GetAlbumTracks_by_release_group_falls_back_to_any_release_when_none_is_official()
	{
		SetupReleases(Browsed("rel-promo", "Promotion", "2001-04-01", 13));

		var tracks = await Source().GetAlbumTracks(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.Equal("From rel-promo", Assert.Single(tracks).Title);
	}

	[Fact]
	public async Task GetAlbumTracks_by_release_group_returns_nothing_when_the_group_has_no_releases()
	{
		SetupReleases();

		var tracks = await Source().GetAlbumTracks(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.Empty(tracks);
		_client.Verify(client => client.GetRelease(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	private void SetupReleases(params Release[] releases)
	{
		_client.Setup(client => client.BrowseReleases("rg-lateralus", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(releases);

		foreach (var release in releases)
		{
			_client.Setup(client => client.GetRelease(release.Id, It.IsAny<CancellationToken>()))
				.ReturnsAsync(release with { Media = [Medium(1, Track(1, $"From {release.Id}", "rec-1", null))] });
		}
	}

	private static Release Browsed(string id, string status, string? date, int trackCount)
	{
		return new Release { Id = id, Title = "Lateralus", Status = status, Date = date, Media = [new Medium { Position = 1, TrackCount = trackCount }] };
	}

	private static Medium Medium(int position, params Track[] tracks)
	{
		return new Medium { Position = position, TrackCount = tracks.Length, Tracks = tracks };
	}

	private static Track Track(int position, string title, string recordingId, int? length, params Artist[] artists)
	{
		return new Track { Position = position, Title = title, Length = length, Recording = new Recording { Id = recordingId, ArtistCredit = artists.Select(Credit).ToList() } };
	}

	private static ArtistCredit Credit(Artist artist)
	{
		return new ArtistCredit { Name = artist.Name, Artist = artist };
	}

	[Fact]
	public async Task GetAlbum_by_release_returns_null_when_the_release_is_not_found()
	{
		_client.Setup(client => client.GetRelease("rel-missing", It.IsAny<CancellationToken>())).ReturnsAsync((Release?)null);

		var album = await Source().GetAlbum(new MusicBrainzReleaseId("rel-missing"), CancellationToken.None);

		Assert.Null(album);
	}

	[Fact]
	public async Task GetAlbum_by_release_group_returns_the_group()
	{
		_client.Setup(client => client.GetReleaseGroup("rg-lateralus", It.IsAny<CancellationToken>())).ReturnsAsync(Lateralus);

		var album = await Source().GetAlbum(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.NotNull(album);
		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus"), album.ReleaseGroupId);
	}

	[Fact]
	public async Task GetAlbum_by_release_group_returns_null_when_the_group_is_not_found()
	{
		_client.Setup(client => client.GetReleaseGroup("rg-missing", It.IsAny<CancellationToken>())).ReturnsAsync((ReleaseGroup?)null);

		var album = await Source().GetAlbum(new MusicBrainzReleaseGroupId("rg-missing"), CancellationToken.None);

		Assert.Null(album);
	}

	[Fact]
	public async Task GetAlbum_maps_credits_in_order_and_an_empty_disambiguation_to_null()
	{
		var split = Lateralus with
		{
			ArtistCredit = [new ArtistCredit { Name = "Tool", Artist = Tool }, new ArtistCredit { Name = "Puscifer", Artist = Puscifer }],
		};
		_client.Setup(client => client.GetReleaseGroup("rg-lateralus", It.IsAny<CancellationToken>())).ReturnsAsync(split);

		var album = await Source().GetAlbum(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.Equal(
			[
				new DiscographyArtist(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band"),
				new DiscographyArtist(new MusicBrainzArtistId("mb-puscifer"), "Puscifer", null),
			],
			album!.Artists);
	}

	[Fact]
	public async Task GetAlbum_is_a_compilation_when_credited_to_various_artists()
	{
		var variousArtists = new Artist { Id = "89ad4ac3-39f7-470e-963a-56509c546377", Name = "Various Artists" };
		_client.Setup(client => client.GetReleaseGroup("rg-lateralus", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Lateralus with { ArtistCredit = [new ArtistCredit { Name = "Various Artists", Artist = variousArtists }] });

		var album = await Source().GetAlbum(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.True(album!.IsCompilation);
	}

	[Theory]
	[InlineData("2001-05-14", 2001)]
	[InlineData("2002-02", 2002)]
	[InlineData("1996", 1996)]
	[InlineData("", null)]
	[InlineData(null, null)]
	public async Task GetAlbum_takes_the_year_from_the_first_release_date(string? firstReleaseDate, int? year)
	{
		_client.Setup(client => client.GetReleaseGroup("rg-lateralus", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Lateralus with { FirstReleaseDate = firstReleaseDate });

		var album = await Source().GetAlbum(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.Equal(year, album!.Year);
	}

	[Fact]
	public async Task SearchAlbums_searches_release_groups_by_the_artist_id_and_title()
	{
		SetupReleaseGroupSearch();

		await Source(searchSize: 10).SearchAlbums(new MusicBrainzArtistId("mb-tool"), "Lateralus", CancellationToken.None);

		_client.Verify(client => client.SearchReleaseGroups("arid:mb-tool AND releasegroup:\"Lateralus\"", 10, 0, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task SearchAlbums_escapes_lucene_special_characters_in_the_title()
	{
		const string expectedQuery = """
			arid:mb-puscifer AND releasegroup:"\"V\" Is for Vagina\: \(What\?\) AC\/DC"
			""";
		SetupReleaseGroupSearch();

		await Source().SearchAlbums(new MusicBrainzArtistId("mb-puscifer"), "\"V\" Is for Vagina: (What?) AC/DC", CancellationToken.None);

		_client.Verify(client => client.SearchReleaseGroups(expectedQuery, 25, 0, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task SearchAlbums_maps_each_release_group()
	{
		SetupReleaseGroupSearch(
			Lateralus,
			Lateralus with { Id = "rg-lateralus-single", FirstReleaseDate = "2002-02" });

		var albums = await Source().SearchAlbums(new MusicBrainzArtistId("mb-tool"), "Lateralus", CancellationToken.None);

		Assert.Equal(2, albums.Count);
		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus"), albums[0].ReleaseGroupId);
		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus-single"), albums[1].ReleaseGroupId);
		Assert.Equal(2002, albums[1].Year);
	}

	[Fact]
	public async Task SearchAlbums_rejects_an_artist_id_that_is_not_a_MusicBrainz_artist_id()
	{
		await Assert.ThrowsAsync<ArgumentException>(() => Source().SearchAlbums(new SubsonicId("art-tool"), "Lateralus", CancellationToken.None));

		_client.Verify(client => client.SearchReleaseGroups(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetArtist_returns_the_artist_with_an_empty_disambiguation_as_null()
	{
		_client.Setup(client => client.GetArtist("mb-puscifer", It.IsAny<CancellationToken>())).ReturnsAsync(Puscifer);

		var artist = await Source().GetArtist(new MusicBrainzArtistId("mb-puscifer"), CancellationToken.None);

		Assert.Equal(new DiscographyArtist(new MusicBrainzArtistId("mb-puscifer"), "Puscifer", null), artist);
	}

	[Fact]
	public async Task GetArtist_returns_null_when_the_artist_is_not_found()
	{
		_client.Setup(client => client.GetArtist("mb-tool", It.IsAny<CancellationToken>())).ReturnsAsync((Artist?)null);

		Assert.Null(await Source().GetArtist(new MusicBrainzArtistId("mb-tool"), CancellationToken.None));
	}

	[Fact]
	public async Task GetArtist_rejects_an_id_that_is_not_a_MusicBrainz_artist_id()
	{
		await Assert.ThrowsAsync<ArgumentException>(() => Source().GetArtist(new MusicBrainzReleaseId("rel-lateralus-cd"), CancellationToken.None));
	}

	[Fact]
	public async Task GetArtistAlbums_browses_the_artists_albums_and_eps_until_a_short_page()
	{
		var opiate = Lateralus with { Id = "rg-opiate", Title = "Opiate", FirstReleaseDate = "1992-03-10" };
		var undertow = Lateralus with { Id = "rg-undertow", Title = "Undertow", FirstReleaseDate = "1993-04-06" };
		_client.Setup(client => client.BrowseReleaseGroups("mb-tool", new[] { "album", "ep" }, 2, 0, It.IsAny<CancellationToken>())).ReturnsAsync([Lateralus, opiate]);
		_client.Setup(client => client.BrowseReleaseGroups("mb-tool", new[] { "album", "ep" }, 2, 2, It.IsAny<CancellationToken>())).ReturnsAsync([undertow]);

		var albums = await Source(pageSize: 2).GetArtistAlbums(new MusicBrainzArtistId("mb-tool"), CancellationToken.None);

		Assert.Equal(["Lateralus", "Opiate", "Undertow"], albums.Select(album => album.Title));
		_client.Verify(client => client.BrowseReleaseGroups(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
	}

	[Fact]
	public async Task GetArtistAlbums_leaves_out_release_groups_with_a_secondary_type()
	{
		var salival = Lateralus with { Id = "rg-salival", Title = "Salival", SecondaryTypes = ["Compilation", "Live"] };
		var opiate = Lateralus with { Id = "rg-opiate", Title = "Opiate", SecondaryTypes = [] };
		_client.Setup(client => client.BrowseReleaseGroups("mb-tool", It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), 0, It.IsAny<CancellationToken>())).ReturnsAsync([Lateralus, salival, opiate]);

		var albums = await Source().GetArtistAlbums(new MusicBrainzArtistId("mb-tool"), CancellationToken.None);

		Assert.Equal(["Lateralus", "Opiate"], albums.Select(album => album.Title));
	}

	[Fact]
	public async Task GetArtistAlbums_maps_each_release_group()
	{
		_client.Setup(client => client.BrowseReleaseGroups("mb-tool", It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), 0, It.IsAny<CancellationToken>())).ReturnsAsync([Lateralus]);

		var album = Assert.Single(await Source().GetArtistAlbums(new MusicBrainzArtistId("mb-tool"), CancellationToken.None));

		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus"), album.ReleaseGroupId);
		Assert.Equal(2001, album.Year);
		Assert.Equal([new DiscographyArtist(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band")], album.Artists);
	}

	[Fact]
	public async Task GetArtistAlbums_stops_and_fails_once_the_artist_has_more_pages_than_allowed()
	{
		_client.Setup(client => client.BrowseReleaseGroups("mb-tool", It.IsAny<IReadOnlyList<string>>(), 2, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([Lateralus, Lateralus]);

		var exception = await Assert.ThrowsAsync<MusicBrainzException>(
			() => Source(pageSize: 2, maxReleaseGroupPages: 3).GetArtistAlbums(new MusicBrainzArtistId("mb-tool"), CancellationToken.None));

		Assert.Equal("Artist mb-tool has more than 6 release groups.", exception.Message);
		_client.Verify(client => client.BrowseReleaseGroups(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
	}

	[Fact]
	public async Task GetArtistAlbums_rejects_an_artist_id_that_is_not_a_MusicBrainz_artist_id()
	{
		await Assert.ThrowsAsync<ArgumentException>(() => Source().GetArtistAlbums(new SubsonicId("art-tool"), CancellationToken.None));
	}

	[Fact]
	public async Task SearchArtists_searches_by_the_escaped_name()
	{
		SetupArtistSearch();

		await Source(searchSize: 10).SearchArtists("AC/DC", CancellationToken.None);

		_client.Verify(client => client.SearchArtists("artist:\"AC\\/DC\"", 10, 0, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task SearchArtists_maps_the_matches_in_order()
	{
		SetupArtistSearch(Tool, Puscifer);

		var artists = await Source().SearchArtists("Tool", CancellationToken.None);

		Assert.Equal(
			[
				new DiscographyArtist(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band"),
				new DiscographyArtist(new MusicBrainzArtistId("mb-puscifer"), "Puscifer", null),
			],
			artists);
	}

	private static Release Release(string id)
	{
		return new Release { Id = id, Title = "Lateralus" };
	}

	private void SetupReleaseGroupSearch(params ReleaseGroup[] releaseGroups)
	{
		_client.Setup(client => client.SearchReleaseGroups(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(releaseGroups);
	}

	private void SetupArtistSearch(params Artist[] artists)
	{
		_client.Setup(client => client.SearchArtists(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(artists);
	}
}
