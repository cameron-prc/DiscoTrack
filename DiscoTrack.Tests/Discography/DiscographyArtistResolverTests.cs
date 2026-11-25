using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using Moq;

namespace DiscoTrack.Tests.Discography;

public class DiscographyArtistResolverTests
{
	private static readonly DiscographyArtist Tool = new(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band");
	private static readonly DiscographyArtist OtherTool = new(new MusicBrainzArtistId("mb-tool-other"), "Tool", "Danish duo");
	private static readonly DiscographyAlbum Lateralus = new(new MusicBrainzReleaseGroupId("rg-lateralus"), [], "Lateralus", 2001, IsCompilation: false, [Tool]);
	private static readonly DiscographyAlbum Undertow = new(new MusicBrainzReleaseGroupId("rg-undertow"), [], "Undertow", 1993, IsCompilation: false, [Tool]);

	private readonly Mock<IDiscographySource> _source = new();
	private readonly Artist _artist = new() { ExternalIds = [new SubsonicId("art-tool")], Name = "TOOL!" };
	private readonly List<Album> _albums = [new Album { Title = "LATERALUS!", Year = 2001 }];

	public DiscographyArtistResolverTests()
	{
		_source.SetupGet(source => source.ArtistProvider).Returns(Provider.MusicBrainzArtist);
		_source.Setup(source => source.SearchArtists(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		_source.Setup(source => source.GetArtistAlbums(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
	}

	private Task<DiscographyArtist?> Resolve()
	{
		return new DiscographyArtistResolver(_source.Object).Resolve(_artist, _albums, CancellationToken.None);
	}

	private void SetupSearch(params DiscographyArtist[] artists)
	{
		_source.Setup(source => source.SearchArtists(_artist.Name, It.IsAny<CancellationToken>())).ReturnsAsync(artists);
	}

	private void SetupAlbums(DiscographyArtist artist, params DiscographyAlbum[] albums)
	{
		_source.Setup(source => source.GetArtistAlbums(artist.Id, It.IsAny<CancellationToken>())).ReturnsAsync(albums);
	}

	[Fact]
	public async Task Gets_the_artist_by_the_artist_id_the_artist_holds()
	{
		_artist.ExternalIds = [new SubsonicId("art-tool"), Tool.Id];
		_source.Setup(source => source.GetArtist(Tool.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Tool);

		Assert.Same(Tool, await Resolve());
		_source.Verify(source => source.SearchArtists(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		_source.Verify(source => source.GetArtistAlbums(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Does_not_search_when_the_artist_holds_an_id_the_discography_does_not_know()
	{
		_artist.ExternalIds = [Tool.Id];
		_source.Setup(source => source.GetArtist(Tool.Id, It.IsAny<CancellationToken>())).ReturnsAsync((DiscographyArtist?)null);

		Assert.Null(await Resolve());
		_source.Verify(source => source.SearchArtists(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Takes_the_name_match_with_an_album_whose_title_matches_one_of_the_artists_albums()
	{
		SetupSearch(OtherTool, Tool);
		SetupAlbums(OtherTool, Undertow with { Title = "Something Else" });
		SetupAlbums(Tool, Undertow, Lateralus);

		Assert.Same(Tool, await Resolve());
	}

	[Fact]
	public async Task Leaves_the_artist_unresolved_when_the_only_name_match_has_no_matching_album()
	{
		SetupSearch(Tool);
		SetupAlbums(Tool, Undertow);

		Assert.Null(await Resolve());
	}

	[Fact]
	public async Task Leaves_the_artist_unresolved_when_several_name_matches_have_a_matching_album()
	{
		SetupSearch(Tool, OtherTool);
		SetupAlbums(Tool, Lateralus);
		SetupAlbums(OtherTool, Lateralus with { ReleaseGroupId = new MusicBrainzReleaseGroupId("rg-lateralus-other") });

		Assert.Null(await Resolve());
	}

	[Fact]
	public async Task Does_not_get_the_albums_of_a_search_result_whose_name_does_not_match()
	{
		var toolband = new DiscographyArtist(new MusicBrainzArtistId("mb-toolband"), "Tool Band", null);
		SetupSearch(toolband, Tool);
		SetupAlbums(Tool, Lateralus);

		Assert.Same(Tool, await Resolve());
		_source.Verify(source => source.GetArtistAlbums(toolband.Id, It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Takes_the_only_name_match_when_the_artist_has_no_albums()
	{
		_albums.Clear();
		SetupSearch(Tool);

		Assert.Same(Tool, await Resolve());
		_source.Verify(source => source.GetArtistAlbums(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Leaves_the_artist_unresolved_when_it_has_no_albums_and_several_names_match()
	{
		_albums.Clear();
		SetupSearch(Tool, OtherTool);

		Assert.Null(await Resolve());
	}

	[Fact]
	public async Task Leaves_the_artist_unresolved_when_no_name_matches()
	{
		SetupSearch(new DiscographyArtist(new MusicBrainzArtistId("mb-toolband"), "Tool Band", null));

		Assert.Null(await Resolve());
	}

	[Fact]
	public async Task Lets_a_source_failure_propagate()
	{
		_source.Setup(source => source.SearchArtists(_artist.Name, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("service unavailable"));

		await Assert.ThrowsAsync<InvalidOperationException>(Resolve);
	}
}
