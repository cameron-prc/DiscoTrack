using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.Discography;
using DiscoTrack.Discography.SyncArtistWithDiscography;
using DiscoTrack.ExternalIds;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AlbumSync = DiscoTrack.Discography.SyncAlbumWithDiscography;

namespace DiscoTrack.Tests.Discography;

public class SyncArtistWithDiscographyServiceTests
{
	private static readonly MusicBrainzArtistId ToolId = new("mb-tool");
	private static readonly DiscographyArtist DiscographyTool = new(ToolId, "Tool", "American rock band");
	private static readonly DiscographyArtist DiscographyPuscifer = new(new MusicBrainzArtistId("mb-puscifer"), "Puscifer", null);
	private static readonly MusicBrainzReleaseGroupId LateralusId = new("rg-lateralus");
	private static readonly DiscographyAlbum DiscographyLateralus = new(LateralusId, [], "Lateralus", 2001, IsCompilation: false, [DiscographyTool]);
	private static readonly DiscographyAlbum DiscographyUndertow = new(new MusicBrainzReleaseGroupId("rg-undertow"), [], "Undertow", 1993, IsCompilation: false, [DiscographyTool]);

	private readonly Mock<ICatalogue> _catalogue = new();
	private readonly Mock<IDiscographySource> _source = new();
	private readonly Mock<IDiscographyArtistResolver> _resolver = new();
	private readonly Mock<AlbumSync.ISyncAlbumWithDiscographyService> _syncAlbum = new();
	private readonly List<string> _calls = [];
	private readonly Artist _tool = new() { ExternalIds = [new SubsonicId("art-tool")], Name = "TOOL" };
	private readonly Album _lateralus;
	private readonly List<Album> _artistAlbums;
	private readonly List<Album> _knownAlbums;
	private readonly List<Artist> _knownArtists;

	public SyncArtistWithDiscographyServiceTests()
	{
		_lateralus = new Album
		{
			ExternalIds = [new SubsonicId("alb-lateralus"), LateralusId],
			Title = "Lateralus",
			Year = 2001,
			Tracked = true,
			Artists = [new AlbumArtist { Artist = _tool, Position = 1 }],
		};
		_artistAlbums = [_lateralus];
		_knownAlbums = [_lateralus];
		_knownArtists = [_tool];

		_source.SetupGet(source => source.ReleaseGroupProvider).Returns(Provider.MusicBrainzReleaseGroup);
		_source.SetupGet(source => source.ArtistProvider).Returns(Provider.MusicBrainzArtist);
		_source.Setup(source => source.GetArtistAlbums(ToolId, It.IsAny<CancellationToken>())).ReturnsAsync([DiscographyLateralus]);
		_resolver.Setup(resolver => resolver.Resolve(_tool, _artistAlbums, It.IsAny<CancellationToken>())).ReturnsAsync(DiscographyTool);
		_syncAlbum.Setup(syncAlbum => syncAlbum.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()))
			.Callback(() => _calls.Add(nameof(AlbumSync.ISyncAlbumWithDiscographyService.Run)))
			.ReturnsAsync((AlbumId albumId, CancellationToken _) => new AlbumSync.SyncAlbumWithDiscographyResponse(_artistAlbums.Single(album => album.Id == albumId), [], [], [], []));

		_catalogue.Setup(catalogue => catalogue.GetArtist(_tool.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_tool);
		_catalogue.Setup(catalogue => catalogue.GetArtistAlbums(_tool.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_artistAlbums);
		_catalogue.Setup(catalogue => catalogue.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync(_knownAlbums);
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync(_knownArtists);
		_catalogue.Setup(catalogue => catalogue.SaveArtist(It.IsAny<Artist>(), It.IsAny<CancellationToken>()))
			.Callback(() => _calls.Add(nameof(ICatalogue.SaveArtist)));
		_catalogue.Setup(catalogue => catalogue.SaveAlbum(It.IsAny<Album>(), It.IsAny<CancellationToken>()))
			.Callback(() => _calls.Add(nameof(ICatalogue.SaveAlbum)));
	}

	private Task<Either<IList<ISyncArtistWithDiscographyError>, SyncArtistWithDiscographyResponse>> Run()
	{
		return new SyncArtistWithDiscographyService(_source.Object, _resolver.Object, _syncAlbum.Object, _catalogue.Object, NullLogger<SyncArtistWithDiscographyService>.Instance)
			.Run(_tool.Id, CancellationToken.None);
	}

	private void SetupDiscography(params DiscographyAlbum[] albums)
	{
		_source.Setup(source => source.GetArtistAlbums(ToolId, It.IsAny<CancellationToken>())).ReturnsAsync(albums);
	}

	[Fact]
	public async Task Saves_the_discography_artist_id_and_name_on_the_artist_before_syncing_its_albums()
	{
		var result = await Run();

		Assert.Equal([new SubsonicId("art-tool"), ToolId], result.GetRight.Artist.ExternalIds);
		Assert.Equal("Tool", result.GetRight.Artist.Name);
		_catalogue.Verify(catalogue => catalogue.SaveArtist(_tool, It.IsAny<CancellationToken>()), Times.Once);
		Assert.Equal([nameof(ICatalogue.SaveArtist), nameof(AlbumSync.ISyncAlbumWithDiscographyService.Run)], _calls);
	}

	[Fact]
	public async Task Does_not_save_the_artist_when_it_already_holds_the_artist_id_and_name()
	{
		_tool.ExternalIds = [new SubsonicId("art-tool"), ToolId];
		_tool.Name = "Tool";

		await Run();

		_catalogue.Verify(catalogue => catalogue.SaveArtist(It.IsAny<Artist>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Saves_the_discography_name_on_an_artist_that_already_holds_the_artist_id()
	{
		_tool.ExternalIds = [new SubsonicId("art-tool"), ToolId];

		var result = await Run();

		Assert.Equal("Tool", result.GetRight.Artist.Name);
		_catalogue.Verify(catalogue => catalogue.SaveArtist(_tool, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Syncs_each_of_the_artists_albums_and_reports_the_ones_that_fail()
	{
		var salival = new Album { Title = "Salival", Year = 2000, Artists = [new AlbumArtist { Artist = _tool, Position = 1 }] };
		_artistAlbums.Add(salival);
		_syncAlbum.Setup(syncAlbum => syncAlbum.Run(salival.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Either<IList<AlbumSync.ISyncAlbumWithDiscographyError>, AlbumSync.SyncAlbumWithDiscographyResponse>.Left([new AlbumSync.UnableToFindDiscographyAlbumError()]));

		var response = (await Run()).GetRight;

		Assert.Equal([_lateralus], response.SyncedAlbums);
		Assert.Equal([salival], response.UnsyncedAlbums);
	}

	[Fact]
	public async Task Adds_a_discography_album_the_catalogue_does_not_hold_as_an_untracked_album()
	{
		SetupDiscography(DiscographyLateralus, DiscographyUndertow);

		var response = (await Run()).GetRight;

		var created = Assert.Single(response.CreatedAlbums);
		Assert.Equal("Undertow", created.Title);
		Assert.Equal(1993, created.Year);
		Assert.False(created.IsCompilation);
		Assert.False(created.Tracked);
		Assert.Equal([DiscographyUndertow.ReleaseGroupId], created.ExternalIds);
		var credit = Assert.Single(created.Artists);
		Assert.Same(_tool, credit.Artist);
		Assert.Equal(1, credit.Position);
		_catalogue.Verify(catalogue => catalogue.SaveAlbum(created, It.IsAny<CancellationToken>()), Times.Once);
		_catalogue.Verify(catalogue => catalogue.SaveAlbum(It.IsAny<Album>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Does_not_add_a_discography_album_held_by_an_album_of_another_artist()
	{
		SetupDiscography(DiscographyUndertow);
		_knownAlbums.Add(new Album { ExternalIds = [DiscographyUndertow.ReleaseGroupId], Title = "Undertow", Year = 1993 });

		var response = (await Run()).GetRight;

		Assert.Empty(response.CreatedAlbums);
		Assert.Empty(response.SkippedAlbums);
	}

	[Fact]
	public async Task Skips_a_discography_album_whose_title_matches_an_album_of_the_artist_that_holds_no_release_group_id()
	{
		SetupDiscography(DiscographyUndertow);
		_knownAlbums.Add(new Album { Title = "UNDERTOW!", Year = 1993, Artists = [new AlbumArtist { Artist = _tool, Position = 1 }] });

		var response = (await Run()).GetRight;

		Assert.Empty(response.CreatedAlbums);
		Assert.Equal([DiscographyUndertow], response.SkippedAlbums);
	}

	[Fact]
	public async Task Skips_a_discography_album_with_no_year()
	{
		var unreleased = DiscographyUndertow with { Year = null };
		SetupDiscography(unreleased);

		var response = (await Run()).GetRight;

		Assert.Empty(response.CreatedAlbums);
		Assert.Equal([unreleased], response.SkippedAlbums);
	}

	[Fact]
	public async Task Credits_a_known_artist_by_its_artist_id_and_creates_the_ones_it_does_not_know()
	{
		var maynard = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-maynard")], Name = "Maynard James Keenan" };
		_knownArtists.Add(maynard);
		SetupDiscography(DiscographyUndertow with { Artists = [DiscographyTool, new DiscographyArtist(new MusicBrainzArtistId("mb-maynard"), "MJK", null), DiscographyPuscifer] });

		var response = (await Run()).GetRight;

		var puscifer = Assert.Single(response.CreatedArtists);
		Assert.Equal("Puscifer", puscifer.Name);
		Assert.Equal([DiscographyPuscifer.Id], puscifer.ExternalIds);
		var created = Assert.Single(response.CreatedAlbums);
		Assert.Equal([_tool, maynard, puscifer], created.Artists.Select(albumArtist => albumArtist.Artist));
		Assert.Equal([1, 2, 3], created.Artists.Select(albumArtist => albumArtist.Position));
		_catalogue.Verify(catalogue => catalogue.SaveArtist(puscifer, It.IsAny<CancellationToken>()), Times.Once);
		Assert.Equal([nameof(ICatalogue.SaveArtist), nameof(ICatalogue.SaveAlbum)], _calls.TakeLast(2));
	}

	[Fact]
	public async Task Creates_an_artist_credited_on_several_new_albums_once()
	{
		SetupDiscography(
			DiscographyUndertow with { Artists = [DiscographyTool, DiscographyPuscifer] },
			new DiscographyAlbum(new MusicBrainzReleaseGroupId("rg-opiate"), [], "Opiate", 1992, IsCompilation: false, [DiscographyPuscifer, DiscographyTool]));

		var response = (await Run()).GetRight;

		var puscifer = Assert.Single(response.CreatedArtists);
		Assert.All(response.CreatedAlbums, album => Assert.Contains(puscifer, album.Artists.Select(albumArtist => albumArtist.Artist)));
	}

	[Fact]
	public async Task Returns_an_error_when_the_catalogue_cannot_be_read()
	{
		_catalogue.Setup(catalogue => catalogue.GetArtist(_tool.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		var error = Assert.Single((await Run()).GetLeft);

		Assert.Equal(new UnableToConnectToCatalogueError("database is locked"), error);
	}

	[Fact]
	public async Task Returns_an_error_when_the_artist_is_not_in_the_catalogue()
	{
		_catalogue.Setup(catalogue => catalogue.GetArtist(_tool.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Artist?)null);

		Assert.IsType<UnableToFindArtistError>(Assert.Single((await Run()).GetLeft));
	}

	[Fact]
	public async Task Returns_an_error_when_the_artist_cannot_be_resolved_in_the_discography()
	{
		_resolver.Setup(resolver => resolver.Resolve(_tool, _artistAlbums, It.IsAny<CancellationToken>())).ReturnsAsync((DiscographyArtist?)null);

		Assert.IsType<UnableToFindDiscographyArtistError>(Assert.Single((await Run()).GetLeft));
		_syncAlbum.Verify(syncAlbum => syncAlbum.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
		_catalogue.Verify(catalogue => catalogue.SaveArtist(It.IsAny<Artist>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Returns_an_error_when_resolving_the_artist_fails()
	{
		_resolver.Setup(resolver => resolver.Resolve(_tool, _artistAlbums, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("service unavailable"));

		Assert.Equal(new UnableToConnectToDiscographyError("service unavailable"), Assert.Single((await Run()).GetLeft));
	}

	[Fact]
	public async Task Returns_an_error_when_the_discography_albums_cannot_be_read()
	{
		_source.Setup(source => source.GetArtistAlbums(ToolId, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("service unavailable"));

		Assert.Equal(new UnableToConnectToDiscographyError("service unavailable"), Assert.Single((await Run()).GetLeft));
	}

	[Fact]
	public async Task Returns_an_error_when_the_resolved_artist_cannot_be_saved()
	{
		_catalogue.Setup(catalogue => catalogue.SaveArtist(_tool, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		Assert.Equal(new UnableToSaveArtistError("database is locked"), Assert.Single((await Run()).GetLeft));
		_syncAlbum.Verify(syncAlbum => syncAlbum.Run(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Returns_an_error_when_a_new_album_cannot_be_saved()
	{
		SetupDiscography(DiscographyUndertow);
		_catalogue.Setup(catalogue => catalogue.SaveAlbum(It.IsAny<Album>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database is locked"));

		Assert.Equal(new UnableToSaveAlbumError("database is locked"), Assert.Single((await Run()).GetLeft));
	}
}
