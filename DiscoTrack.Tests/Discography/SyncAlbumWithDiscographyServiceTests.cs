using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Discography;
using DiscoTrack.Discography.SyncAlbumWithDiscography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests.Discography;

public class SyncAlbumWithDiscographyServiceTests
{
	private static readonly MusicBrainzReleaseId ReleaseId = new("rel-lateralus-cd");
	private static readonly MusicBrainzReleaseGroupId ReleaseGroupId = new("rg-lateralus");
	private static readonly DiscographyArtist DiscographyTool = new(new MusicBrainzArtistId("mb-tool"), "Tool", "American rock band");
	private static readonly DiscographyArtist DiscographyPuscifer = new(new MusicBrainzArtistId("mb-puscifer"), "Puscifer", null);

	private readonly Mock<ICatalogue> _catalogue = new();
	private readonly Mock<IDiscographySource> _source = new();
	private readonly Mock<IDiscographyAlbumResolver> _resolver = new();
	private readonly List<string> _saves = [];
	private readonly Artist _tool = new() { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
	private readonly Album _album;
	private readonly List<Album> _knownAlbums;

	public SyncAlbumWithDiscographyServiceTests()
	{
		_album = new Album
		{
			ExternalIds = [new SubsonicId("alb-lateralus"), ReleaseId],
			Title = "Lateralus (Remaster)",
			Year = 2019,
			Artists = [new AlbumArtist { Artist = _tool, Position = 1 }],
		};

		_knownAlbums = [_album];

		_source.SetupGet(source => source.ReleaseGroupProvider).Returns(Provider.MusicBrainzReleaseGroup);
		_source.SetupGet(source => source.ArtistProvider).Returns(Provider.MusicBrainzArtist);
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>())).ReturnsAsync(Discography(DiscographyTool));

		_catalogue.Setup(catalogue => catalogue.GetAlbum(_album.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_album);
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([_tool]);
		_catalogue.Setup(catalogue => catalogue.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync(_knownAlbums);
		_catalogue.Setup(catalogue => catalogue.GetAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		_catalogue.Setup(catalogue => catalogue.MergeAlbums(It.IsAny<Album>(), It.IsAny<AlbumId>(), It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()))
			.Callback(() => _saves.Add(nameof(ICatalogue.MergeAlbums)));
		_catalogue.Setup(catalogue => catalogue.SaveArtist(It.IsAny<Artist>(), It.IsAny<CancellationToken>()))
			.Callback(() => _saves.Add(nameof(ICatalogue.SaveArtist)));
		_catalogue.Setup(catalogue => catalogue.SaveAlbum(It.IsAny<Album>(), It.IsAny<CancellationToken>()))
			.Callback(() => _saves.Add(nameof(ICatalogue.SaveAlbum)));
	}

	private SyncAlbumWithDiscographyService Sync()
	{
		return new SyncAlbumWithDiscographyService(_source.Object, _resolver.Object, _catalogue.Object, NullLogger<SyncAlbumWithDiscographyService>.Instance);
	}

	private static DiscographyAlbum Discography(params DiscographyArtist[] artists)
	{
		return new DiscographyAlbum(ReleaseGroupId, "Lateralus", 2001, IsCompilation: false, artists);
	}

	[Fact]
	public async Task Takes_title_year_and_compilation_flag_from_the_discography_and_keeps_the_album_id()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool) with { IsCompilation = true });

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var synced = result.GetRight.Album;
		Assert.Equal(_album.Id, synced.Id);
		Assert.Equal("Lateralus", synced.Title);
		Assert.Equal(2001, synced.Year);
		Assert.True(synced.IsCompilation);
		_catalogue.Verify(catalogue => catalogue.SaveAlbum(synced, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Keeps_the_album_tracked_flag(bool tracked)
	{
		_album.Tracked = tracked;
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>())).ReturnsAsync(Discography(DiscographyTool));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(tracked, result.GetRight.Album.Tracked);
	}

	[Fact]
	public async Task Keeps_the_album_year_when_the_discography_has_none()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool) with { Year = null });

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(2019, result.GetRight.Album.Year);
	}

	[Fact]
	public async Task Adds_the_release_group_id_and_keeps_the_ids_the_album_already_holds()
	{
		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal([new SubsonicId("alb-lateralus"), ReleaseId, ReleaseGroupId], result.GetRight.Album.ExternalIds);
	}

	[Fact]
	public async Task Replaces_a_release_group_id_the_album_already_holds()
	{
		_album.ExternalIds = [new SubsonicId("alb-lateralus"), new MusicBrainzReleaseGroupId("rg-stale"), ReleaseId];

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal([new SubsonicId("alb-lateralus"), ReleaseId, ReleaseGroupId], result.GetRight.Album.ExternalIds);
	}

	[Fact]
	public async Task Absorbs_another_album_holding_the_release_group_id_keeping_the_ids_of_both()
	{
		var otherEdition = new Album
		{
			ExternalIds = [new SubsonicId("alb-lateralus-vinyl"), new MusicBrainzReleaseId("rel-lateralus-vinyl"), ReleaseGroupId],
			Title = "Lateralus",
			Year = 2001,
		};
		_knownAlbums.Add(otherEdition);

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		Assert.Equal(_album.Id, response.Album.Id);
		Assert.Same(otherEdition, response.AbsorbedAlbum);
		Assert.Equal(
			[new SubsonicId("alb-lateralus"), ReleaseId, new SubsonicId("alb-lateralus-vinyl"), new MusicBrainzReleaseId("rel-lateralus-vinyl"), ReleaseGroupId],
			response.Album.ExternalIds);
		_catalogue.Verify(catalogue => catalogue.MergeAlbums(response.Album, otherEdition.Id, It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()), Times.Once);
		Assert.Equal([nameof(ICatalogue.MergeAlbums)], _saves);
	}

	[Fact]
	public async Task Absorbing_an_album_merges_its_tracks_into_the_albums_own()
	{
		var otherEdition = new Album { ExternalIds = [new SubsonicId("alb-lateralus-vinyl"), ReleaseGroupId], Title = "Lateralus", Year = 2001 };
		_knownAlbums.Add(otherEdition);
		var grudge = new Track { AlbumId = _album.Id, Title = "The Grudge", DiscNumber = 1, Position = 1, ExternalIds = [new SubsonicId("song-grudge")] };
		var vinylGrudge = new Track { AlbumId = otherEdition.Id, Title = "The Grudge", DiscNumber = 1, Position = 1, ExternalIds = [new SubsonicId("song-grudge-vinyl")] };
		var vinylBonus = new Track { AlbumId = otherEdition.Id, Title = "Bonus", DiscNumber = 2, Position = 1, ExternalIds = [new SubsonicId("song-bonus")] };
		_catalogue.Setup(catalogue => catalogue.GetAlbumTracks(_album.Id, It.IsAny<CancellationToken>())).ReturnsAsync([grudge]);
		_catalogue.Setup(catalogue => catalogue.GetAlbumTracks(otherEdition.Id, It.IsAny<CancellationToken>())).ReturnsAsync([vinylGrudge, vinylBonus]);
		IReadOnlyList<Track> merged = [];
		_catalogue.Setup(catalogue => catalogue.MergeAlbums(It.IsAny<Album>(), It.IsAny<AlbumId>(), It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()))
			.Callback((Album _, AlbumId _, IReadOnlyList<Track> tracks, CancellationToken _) => merged = tracks);

		await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(["The Grudge", "Bonus"], merged.Select(track => track.Title));
		Assert.All(merged, track => Assert.Equal(_album.Id, track.AlbumId));
		Assert.Equal([new SubsonicId("song-grudge"), new SubsonicId("song-grudge-vinyl")], merged[0].ExternalIds);
		Assert.Equal(vinylBonus.Id, merged[1].Id);
	}

	[Fact]
	public async Task Does_not_read_tracks_when_no_album_is_absorbed()
	{
		await Sync().Run(_album.Id, CancellationToken.None);

		_catalogue.Verify(catalogue => catalogue.GetAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public async Task Tracks_the_album_when_it_or_the_album_it_absorbs_is_tracked(bool tracked, bool absorbedTracked)
	{
		_album.Tracked = tracked;
		_knownAlbums.Add(new Album { ExternalIds = [ReleaseGroupId], Title = "Lateralus", Year = 2001, Tracked = absorbedTracked });

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		Assert.True(response.Album.Tracked);
	}

	[Fact]
	public async Task Absorbs_nothing_when_only_the_album_itself_holds_the_release_group_id()
	{
		_album.ExternalIds = [new SubsonicId("alb-lateralus"), ReleaseId, ReleaseGroupId];

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		Assert.Null(response.AbsorbedAlbum);
		Assert.Equal([nameof(ICatalogue.SaveAlbum)], _saves);
	}

	[Fact]
	public async Task Returns_an_error_when_absorbing_an_album_fails()
	{
		_knownAlbums.Add(new Album { ExternalIds = [ReleaseGroupId], Title = "Lateralus", Year = 2001 });
		_catalogue.Setup(catalogue => catalogue.MergeAlbums(It.IsAny<Album>(), It.IsAny<AlbumId>(), It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(new UnableToSaveAlbumError("database is locked"), Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Credits_the_catalogue_artist_holding_the_discography_artist_id_without_saving_it()
	{
		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var credit = Assert.Single(result.GetRight.Album.Artists);
		Assert.Same(_tool, credit.Artist);
		Assert.Equal(1, credit.Position);
		Assert.Equal([nameof(ICatalogue.SaveAlbum)], _saves);
	}

	[Fact]
	public async Task Adds_an_artist_the_catalogue_does_not_hold_and_saves_it_before_the_album()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool, DiscographyPuscifer));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var credits = result.GetRight.Album.Artists;
		Assert.Equal([1, 2], credits.Select(credit => credit.Position));
		Assert.Same(_tool, credits[0].Artist);
		Assert.Equal("Puscifer", credits[1].Artist.Name);
		Assert.Equal([new MusicBrainzArtistId("mb-puscifer")], credits[1].Artist.ExternalIds);
		Assert.False(credits[1].Artist.Tracked);
		_catalogue.Verify(catalogue => catalogue.SaveArtist(credits[1].Artist, It.IsAny<CancellationToken>()), Times.Once);
		Assert.Equal([nameof(ICatalogue.SaveArtist), nameof(ICatalogue.SaveAlbum)], _saves);
	}

	[Fact]
	public async Task Gives_the_discography_artist_id_to_an_album_artist_matching_by_name_and_saves_it_before_the_album()
	{
		var puscifer = new Artist { ExternalIds = [new SubsonicId("art-puscifer")], Name = "PUSCIFER" };
		_album.Artists = [new AlbumArtist { Artist = _tool, Position = 1 }, new AlbumArtist { Artist = puscifer, Position = 2 }];
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([_tool, puscifer]);
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool, DiscographyPuscifer));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var response = result.GetRight;
		Assert.Same(puscifer, response.Album.Artists[1].Artist);
		Assert.Equal([new SubsonicId("art-puscifer"), new MusicBrainzArtistId("mb-puscifer")], puscifer.ExternalIds);
		Assert.Equal([puscifer], response.UpdatedArtists);
		Assert.Empty(response.CreatedArtists);
		Assert.Empty(response.LinkedArtists);
		Assert.Empty(response.UnlinkedArtists);
		_catalogue.Verify(catalogue => catalogue.SaveArtist(puscifer, It.IsAny<CancellationToken>()), Times.Once);
		Assert.Equal([nameof(ICatalogue.SaveArtist), nameof(ICatalogue.SaveAlbum)], _saves);
	}

	[Fact]
	public async Task Matches_an_album_artist_by_name_ignoring_punctuation_and_diacritics()
	{
		var puscifer = new Artist { ExternalIds = [new SubsonicId("art-puscifer")], Name = "Pus-cifér!" };
		_album.Artists = [new AlbumArtist { Artist = _tool, Position = 1 }, new AlbumArtist { Artist = puscifer, Position = 2 }];
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([_tool, puscifer]);
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool, DiscographyPuscifer));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal([puscifer], result.GetRight.UpdatedArtists);
		Assert.Empty(result.GetRight.CreatedArtists);
	}

	[Fact]
	public async Task Does_not_match_by_name_an_album_artist_already_holding_a_discography_artist_id()
	{
		var otherPuscifer = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-other-puscifer")], Name = "Puscifer" };
		_album.Artists = [new AlbumArtist { Artist = _tool, Position = 1 }, new AlbumArtist { Artist = otherPuscifer, Position = 2 }];
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([_tool, otherPuscifer]);
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool, DiscographyPuscifer));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var response = result.GetRight;
		Assert.Equal([new MusicBrainzArtistId("mb-other-puscifer")], otherPuscifer.ExternalIds);
		Assert.Empty(response.UpdatedArtists);
		Assert.NotSame(otherPuscifer, Assert.Single(response.CreatedArtists));
		Assert.Equal([otherPuscifer], response.UnlinkedArtists);
	}

	[Fact]
	public async Task Drops_album_artists_the_discography_does_not_credit()
	{
		var untagged = new Artist { ExternalIds = [new SubsonicId("art-untagged")], Name = "Untagged" };
		_album.Artists = [new AlbumArtist { Artist = untagged, Position = 1 }, new AlbumArtist { Artist = _tool, Position = 2 }];
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([untagged, _tool]);

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Same(_tool, Assert.Single(result.GetRight.Album.Artists).Artist);
	}

	[Fact]
	public async Task Reports_nothing_created_linked_or_unlinked_when_the_credits_are_unchanged()
	{
		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var response = result.GetRight;
		Assert.Empty(response.CreatedArtists);
		Assert.Empty(response.UpdatedArtists);
		Assert.Empty(response.LinkedArtists);
		Assert.Empty(response.UnlinkedArtists);
	}

	[Fact]
	public async Task Reports_a_created_artist_as_both_created_and_linked()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool, DiscographyPuscifer));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var response = result.GetRight;
		var puscifer = response.Album.Artists[1].Artist;
		Assert.Equal([puscifer], response.CreatedArtists);
		Assert.Equal([puscifer], response.LinkedArtists);
		Assert.Empty(response.UnlinkedArtists);
	}

	[Fact]
	public async Task Reports_an_existing_artist_newly_credited_as_linked_but_not_created()
	{
		var puscifer = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-puscifer")], Name = "Puscifer" };
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([_tool, puscifer]);
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool, DiscographyPuscifer));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var response = result.GetRight;
		Assert.Empty(response.CreatedArtists);
		Assert.Equal([puscifer], response.LinkedArtists);
		Assert.Empty(response.UnlinkedArtists);
	}

	[Fact]
	public async Task Reports_album_artists_the_discography_does_not_credit_as_unlinked()
	{
		var untagged = new Artist { ExternalIds = [new SubsonicId("art-untagged")], Name = "Untagged" };
		_album.Artists = [new AlbumArtist { Artist = untagged, Position = 1 }, new AlbumArtist { Artist = _tool, Position = 2 }];
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([untagged, _tool]);

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		var response = result.GetRight;
		Assert.Empty(response.CreatedArtists);
		Assert.Empty(response.LinkedArtists);
		Assert.Equal([untagged], response.UnlinkedArtists);
	}

	[Fact]
	public async Task Returns_an_error_when_the_catalogue_cannot_be_read()
	{
		_catalogue.Setup(catalogue => catalogue.GetAlbum(_album.Id, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(new UnableToConnectToCatalogueError("database is locked"), Assert.Single(result.GetLeft));
		Assert.Empty(_saves);
	}

	[Fact]
	public async Task Returns_an_error_when_the_album_is_not_in_the_catalogue()
	{
		var result = await Sync().Run(AlbumId.New(), CancellationToken.None);

		Assert.IsType<UnableToFindAlbumError>(Assert.Single(result.GetLeft));
		Assert.Empty(_saves);
	}

	[Fact]
	public async Task Returns_an_error_when_the_discography_cannot_be_reached()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("service unavailable"));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(new UnableToConnectToDiscographyError("service unavailable"), Assert.Single(result.GetLeft));
		Assert.Empty(_saves);
	}

	[Fact]
	public async Task Returns_an_error_when_the_album_cannot_be_resolved_in_the_discography()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>())).ReturnsAsync((DiscographyAlbum?)null);

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.IsType<UnableToFindDiscographyAlbumError>(Assert.Single(result.GetLeft));
		Assert.Empty(_saves);
	}

	[Fact]
	public async Task Returns_an_error_and_does_not_save_the_album_when_saving_a_new_artist_fails()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool, DiscographyPuscifer));
		_catalogue.Setup(catalogue => catalogue.SaveArtist(It.IsAny<Artist>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(new UnableToSaveArtistError("database is locked"), Assert.Single(result.GetLeft));
		Assert.Empty(_saves);
	}

	[Fact]
	public async Task Returns_an_error_when_saving_the_album_fails()
	{
		_catalogue.Setup(catalogue => catalogue.SaveAlbum(It.IsAny<Album>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("UNIQUE constraint failed"));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(new UnableToSaveAlbumError("UNIQUE constraint failed"), Assert.Single(result.GetLeft));
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_becoming_an_error()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => Sync().Run(_album.Id, CancellationToken.None));

		Assert.Empty(_saves);
	}
}
