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

	private MusicBrainzDiscographySource Source(int pageSize = 100, int searchSize = 25)
	{
		return new(_client.Object, TestMusicBrainzOptions.Create(pageSize, searchSize));
	}

	[Fact]
	public async Task GetAlbum_by_release_returns_its_release_group_and_every_release_in_it()
	{
		_client.Setup(client => client.GetRelease("rel-lateralus-cd", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Release("rel-lateralus-cd") with { ReleaseGroup = Lateralus });
		SetupReleases("rg-lateralus", "rel-lateralus-cd", "rel-lateralus-vinyl");

		var album = await Source().GetAlbum(new MusicBrainzReleaseId("rel-lateralus-cd"), CancellationToken.None);

		Assert.NotNull(album);
		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus"), album.ReleaseGroupId);
		Assert.Equal([new MusicBrainzReleaseId("rel-lateralus-cd"), new MusicBrainzReleaseId("rel-lateralus-vinyl")], album.ReleaseIds);
		Assert.Equal("Lateralus", album.Title);
		Assert.Equal(2001, album.Year);
		Assert.False(album.IsCompilation);
		Assert.Equal([new DiscographyArtist(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band")], album.Artists);
	}

	[Fact]
	public async Task GetAlbum_by_release_returns_null_when_the_release_is_not_found()
	{
		_client.Setup(client => client.GetRelease("rel-missing", It.IsAny<CancellationToken>())).ReturnsAsync((Release?)null);

		var album = await Source().GetAlbum(new MusicBrainzReleaseId("rel-missing"), CancellationToken.None);

		Assert.Null(album);
		_client.Verify(client => client.BrowseReleases(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetAlbum_by_release_group_returns_the_group_and_every_release_in_it()
	{
		_client.Setup(client => client.GetReleaseGroup("rg-lateralus", It.IsAny<CancellationToken>())).ReturnsAsync(Lateralus);
		SetupReleases("rg-lateralus", "rel-lateralus-cd", "rel-lateralus-vinyl");

		var album = await Source().GetAlbum(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.NotNull(album);
		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus"), album.ReleaseGroupId);
		Assert.Equal([new MusicBrainzReleaseId("rel-lateralus-cd"), new MusicBrainzReleaseId("rel-lateralus-vinyl")], album.ReleaseIds);
	}

	[Fact]
	public async Task GetAlbum_by_release_group_returns_null_when_the_group_is_not_found()
	{
		_client.Setup(client => client.GetReleaseGroup("rg-missing", It.IsAny<CancellationToken>())).ReturnsAsync((ReleaseGroup?)null);

		var album = await Source().GetAlbum(new MusicBrainzReleaseGroupId("rg-missing"), CancellationToken.None);

		Assert.Null(album);
	}

	[Fact]
	public async Task GetAlbum_pages_releases_until_a_short_page()
	{
		_client.Setup(client => client.GetReleaseGroup("rg-lateralus", It.IsAny<CancellationToken>())).ReturnsAsync(Lateralus);
		SetupReleases("rg-lateralus", "1", "2", "3", "4", "5");

		var album = await Source(pageSize: 2).GetAlbum(new MusicBrainzReleaseGroupId("rg-lateralus"), CancellationToken.None);

		Assert.Equal(["1", "2", "3", "4", "5"], album!.ReleaseIds.Select(id => id.Value));
		_client.Verify(client => client.BrowseReleases("rg-lateralus", 2, 0, It.IsAny<CancellationToken>()), Times.Once);
		_client.Verify(client => client.BrowseReleases("rg-lateralus", 2, 2, It.IsAny<CancellationToken>()), Times.Once);
		_client.Verify(client => client.BrowseReleases("rg-lateralus", 2, 4, It.IsAny<CancellationToken>()), Times.Once);
		_client.Verify(client => client.BrowseReleases(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
	}

	[Fact]
	public async Task GetAlbum_maps_credits_in_order_and_an_empty_disambiguation_to_null()
	{
		var split = Lateralus with
		{
			ArtistCredit = [new ArtistCredit { Name = "Tool", Artist = Tool }, new ArtistCredit { Name = "Puscifer", Artist = Puscifer }],
		};
		_client.Setup(client => client.GetReleaseGroup("rg-lateralus", It.IsAny<CancellationToken>())).ReturnsAsync(split);
		SetupReleases("rg-lateralus");

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
		SetupReleases("rg-lateralus");

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
		SetupReleases("rg-lateralus");

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
	public async Task SearchAlbums_maps_each_release_group_with_the_releases_it_came_with()
	{
		SetupReleaseGroupSearch(
			Lateralus with { Releases = [Release("rel-lateralus-cd"), Release("rel-lateralus-vinyl")] },
			Lateralus with { Id = "rg-lateralus-single", FirstReleaseDate = "2002-02", Releases = null });

		var albums = await Source().SearchAlbums(new MusicBrainzArtistId("mb-tool"), "Lateralus", CancellationToken.None);

		Assert.Equal(2, albums.Count);
		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus"), albums[0].ReleaseGroupId);
		Assert.Equal([new MusicBrainzReleaseId("rel-lateralus-cd"), new MusicBrainzReleaseId("rel-lateralus-vinyl")], albums[0].ReleaseIds);
		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus-single"), albums[1].ReleaseGroupId);
		Assert.Empty(albums[1].ReleaseIds);
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
	public async Task GetArtistAlbums_maps_each_release_group_without_releases()
	{
		_client.Setup(client => client.BrowseReleaseGroups("mb-tool", It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), 0, It.IsAny<CancellationToken>())).ReturnsAsync([Lateralus]);

		var album = Assert.Single(await Source().GetArtistAlbums(new MusicBrainzArtistId("mb-tool"), CancellationToken.None));

		Assert.Equal(new MusicBrainzReleaseGroupId("rg-lateralus"), album.ReleaseGroupId);
		Assert.Empty(album.ReleaseIds);
		Assert.Equal(2001, album.Year);
		Assert.Equal([new DiscographyArtist(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band")], album.Artists);
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

	private void SetupReleases(string releaseGroupId, params string[] releaseIds)
	{
		_client.Setup(client => client.BrowseReleases(releaseGroupId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string _, int limit, int offset, CancellationToken _) => releaseIds.Skip(offset).Take(limit).Select(Release).ToList());
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
