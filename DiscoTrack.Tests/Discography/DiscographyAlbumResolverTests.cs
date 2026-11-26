using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using Moq;

namespace DiscoTrack.Tests.Discography;

public class DiscographyAlbumResolverTests
{
	private static readonly MusicBrainzReleaseId ReleaseId = new("rel-lateralus-cd");
	private static readonly MusicBrainzReleaseGroupId ReleaseGroupId = new("rg-lateralus");
	private static readonly MusicBrainzArtistId ToolId = new("mb-tool");
	private static readonly MusicBrainzArtistId PusciferId = new("mb-puscifer");
	private static readonly DiscographyAlbum Lateralus = new(ReleaseGroupId, "Lateralus", 2001, IsCompilation: false, []);

	private readonly Mock<IDiscographySource> _source = new();
	private readonly Artist _tool = new() { ExternalIds = [new SubsonicId("art-tool"), ToolId], Name = "Tool" };
	private readonly Artist _puscifer = new() { ExternalIds = [PusciferId], Name = "Puscifer" };
	private readonly Album _album;

	public DiscographyAlbumResolverTests()
	{
		_album = new Album
		{
			ExternalIds = [new SubsonicId("alb-lateralus")],
			Title = "LATERALUS!",
			Year = 2001,
			Artists = [new AlbumArtist { Artist = _tool, Position = 1 }],
		};

		_source.SetupGet(source => source.ReleaseProvider).Returns(Provider.MusicBrainzRelease);
		_source.SetupGet(source => source.ReleaseGroupProvider).Returns(Provider.MusicBrainzReleaseGroup);
		_source.SetupGet(source => source.ArtistProvider).Returns(Provider.MusicBrainzArtist);
		_source.Setup(source => source.SearchAlbums(It.IsAny<ExternalId>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
	}

	private Task<DiscographyAlbum?> Resolve()
	{
		return new DiscographyAlbumResolver(_source.Object).Resolve(_album, CancellationToken.None);
	}

	private void SetupSearch(ExternalId artistId, params DiscographyAlbum[] albums)
	{
		_source.Setup(source => source.SearchAlbums(artistId, _album.Title, It.IsAny<CancellationToken>())).ReturnsAsync(albums);
	}

	[Fact]
	public async Task Gets_the_album_by_release_id_ahead_of_the_release_group_id()
	{
		_album.ExternalIds = [new SubsonicId("alb-lateralus"), ReleaseGroupId, ReleaseId];
		_source.Setup(source => source.GetAlbum(ReleaseId, It.IsAny<CancellationToken>())).ReturnsAsync(Lateralus);

		var resolved = await Resolve();

		Assert.Same(Lateralus, resolved);
		_source.Verify(source => source.GetAlbum(ReleaseGroupId, It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Gets_the_album_by_release_group_id_when_it_holds_no_release_id()
	{
		_album.ExternalIds = [new SubsonicId("alb-lateralus"), ReleaseGroupId];
		_source.Setup(source => source.GetAlbum(ReleaseGroupId, It.IsAny<CancellationToken>())).ReturnsAsync(Lateralus);

		Assert.Same(Lateralus, await Resolve());
	}

	[Fact]
	public async Task Does_not_search_when_the_album_holds_an_id_the_discography_does_not_know()
	{
		_album.ExternalIds = [ReleaseId];
		_source.Setup(source => source.GetAlbum(ReleaseId, It.IsAny<CancellationToken>())).ReturnsAsync((DiscographyAlbum?)null);

		Assert.Null(await Resolve());
		_source.Verify(source => source.SearchAlbums(It.IsAny<ExternalId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Searches_by_artist_id_and_takes_the_single_album_whose_title_matches()
	{
		SetupSearch(ToolId, Lateralus with { Title = "Lateralus Live" }, Lateralus);

		Assert.Same(Lateralus, await Resolve());
	}

	[Fact]
	public async Task Tries_the_album_artists_in_position_order_until_one_has_a_title_match()
	{
		_album.Artists = [new AlbumArtist { Artist = _tool, Position = 2 }, new AlbumArtist { Artist = _puscifer, Position = 1 }];
		var calls = new List<ExternalId>();
		_source.Setup(source => source.SearchAlbums(It.IsAny<ExternalId>(), _album.Title, It.IsAny<CancellationToken>()))
			.Callback((ExternalId artistId, string _, CancellationToken _) => calls.Add(artistId))
			.ReturnsAsync((ExternalId artistId, string _, CancellationToken _) => artistId == ToolId ? [Lateralus] : []);

		var resolved = await Resolve();

		Assert.Same(Lateralus, resolved);
		Assert.Equal([PusciferId, ToolId], calls);
	}

	[Fact]
	public async Task Stops_at_the_first_artist_with_a_title_match()
	{
		_album.Artists = [new AlbumArtist { Artist = _tool, Position = 1 }, new AlbumArtist { Artist = _puscifer, Position = 2 }];
		SetupSearch(ToolId, Lateralus);

		await Resolve();

		_source.Verify(source => source.SearchAlbums(PusciferId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Takes_the_title_match_from_the_album_year_when_several_match()
	{
		var reissue = Lateralus with { ReleaseGroupId = new MusicBrainzReleaseGroupId("rg-lateralus-reissue"), Year = 2019 };
		SetupSearch(ToolId, reissue, Lateralus);

		Assert.Same(Lateralus, await Resolve());
	}

	[Fact]
	public async Task Leaves_the_album_unresolved_when_several_titles_match_and_the_year_does_not_single_one_out()
	{
		var other = Lateralus with { ReleaseGroupId = new MusicBrainzReleaseGroupId("rg-lateralus-other") };
		SetupSearch(ToolId, other, Lateralus);

		Assert.Null(await Resolve());
	}

	[Fact]
	public async Task Leaves_the_album_unresolved_when_no_title_matches()
	{
		SetupSearch(ToolId, Lateralus with { Title = "Lateralus Live" });

		Assert.Null(await Resolve());
	}

	[Fact]
	public async Task Leaves_the_album_unresolved_without_calling_the_source_when_neither_it_nor_its_artists_hold_an_id()
	{
		_tool.ExternalIds = [new SubsonicId("art-tool")];

		Assert.Null(await Resolve());
		_source.Verify(source => source.GetAlbum(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
		_source.Verify(source => source.SearchAlbums(It.IsAny<ExternalId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Lets_a_source_failure_propagate()
	{
		_source.Setup(source => source.SearchAlbums(ToolId, _album.Title, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("service unavailable"));

		await Assert.ThrowsAsync<InvalidOperationException>(Resolve);
	}
}
