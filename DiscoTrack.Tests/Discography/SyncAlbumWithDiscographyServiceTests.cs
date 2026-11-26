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
	private IReadOnlyList<Track> _savedTracks = [];
	private readonly Artist _tool = new() { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool", Disambiguation = "American rock band" };
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
		_source.SetupGet(source => source.ReleaseProvider).Returns(Provider.MusicBrainzRelease);
		_source.SetupGet(source => source.RecordingProvider).Returns(Provider.MusicBrainzRecording);
		_source.Setup(source => source.GetAlbumTracks(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		_catalogue.Setup(catalogue => catalogue.SaveAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()))
			.Callback((AlbumId _, IReadOnlyList<Track> tracks, CancellationToken _) =>
			{
				_saves.Add(nameof(ICatalogue.SaveAlbumTracks));
				_savedTracks = tracks;
			});
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

	[Theory]
	[InlineData(null)]
	[InlineData("English tool makers")]
	public async Task Saves_a_known_artist_whose_disambiguation_differs_from_the_discography(string? disambiguation)
	{
		_tool.Disambiguation = disambiguation;

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		Assert.Equal("American rock band", _tool.Disambiguation);
		Assert.Equal([_tool], response.UpdatedArtists);
		Assert.Equal([nameof(ICatalogue.SaveArtist), nameof(ICatalogue.SaveAlbum)], _saves);
	}

	[Fact]
	public async Task Gives_a_new_artist_the_discography_disambiguation()
	{
		var other = new DiscographyArtist(new MusicBrainzArtistId("mb-other-tool"), "Tool", "90s trance DJ");
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>())).ReturnsAsync(Discography(other));

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		var created = Assert.Single(response.CreatedArtists);
		Assert.Equal("90s trance DJ", created.Disambiguation);
		Assert.Equal("Tool (90s trance DJ)", created.DisplayName);
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
	public async Task Saves_the_tracks_that_came_with_a_tracked_albums_release_after_the_album_without_asking_again()
	{
		Tracklist(Listed(1, "The Grudge", "rec-grudge"), Listed(2, "Eon Blue Apocalypse", "rec-eon"));

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		Assert.Equal([nameof(ICatalogue.SaveAlbum), nameof(ICatalogue.SaveAlbumTracks)], _saves);
		Assert.Equal(["The Grudge", "Eon Blue Apocalypse"], _savedTracks.Select(track => track.Title));
		Assert.Same(_savedTracks, response.Tracks);
		var grudge = _savedTracks[0];
		Assert.Equal(_album.Id, grudge.AlbumId);
		Assert.Equal((1, 1, 1000), (grudge.DiscNumber, grudge.Position, grudge.DurationMs));
		Assert.Equal([new MusicBrainzRecordingId("rec-grudge")], grudge.ExternalIds);
		Assert.Same(_tool, Assert.Single(grudge.Artists).Artist);
		_source.Verify(source => source.GetAlbumTracks(It.IsAny<ExternalId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Asks_for_the_release_groups_tracks_when_a_tracked_album_holds_no_release()
	{
		_album.Tracked = true;
		_album.ExternalIds = [new SubsonicId("alb-lateralus")];
		_source.Setup(source => source.GetAlbumTracks(ReleaseGroupId, It.IsAny<CancellationToken>())).ReturnsAsync([Listed(1, "The Grudge", "rec-grudge")]);

		await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal("The Grudge", Assert.Single(_savedTracks).Title);
	}

	[Fact]
	public async Task Takes_the_longest_tracklist_when_the_album_holds_several_releases()
	{
		var vinyl = new MusicBrainzReleaseId("rel-lateralus-vinyl");
		_album.ExternalIds = [new SubsonicId("alb-lateralus"), ReleaseId, vinyl];
		Tracklist(Listed(1, "The Grudge", "rec-grudge"));
		_source.Setup(source => source.GetAlbumTracks(vinyl, It.IsAny<CancellationToken>()))
			.ReturnsAsync([Listed(1, "The Grudge", "rec-grudge"), Listed(2, "Bonus", "rec-bonus")]);

		await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(["The Grudge", "Bonus"], _savedTracks.Select(track => track.Title));
		_source.Verify(source => source.GetAlbumTracks(ReleaseId, It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Leaves_the_tracks_of_an_untracked_album_alone()
	{
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool) with { Tracks = [Listed(1, "The Grudge", "rec-grudge")] });

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		Assert.Null(response.Tracks);
		Assert.Equal([nameof(ICatalogue.SaveAlbum)], _saves);
		_catalogue.Verify(catalogue => catalogue.GetAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Leaves_the_tracks_alone_when_the_discography_lists_none()
	{
		Tracklist();
		KnownTracks(Owned(1, "The Grudge", "song-grudge"));

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		Assert.Null(response.Tracks);
		Assert.Equal([nameof(ICatalogue.SaveAlbum)], _saves);
	}

	[Fact]
	public async Task Gives_an_owned_track_at_the_same_place_the_recording_id_title_and_credits_and_keeps_its_library_id()
	{
		var puscifer = new Artist { ExternalIds = [new SubsonicId("art-puscifer")], Name = "Puscifer" };
		var owned = Owned(1, "the grudge", "song-grudge");
		owned.Artists = [new TrackArtist { Artist = puscifer, Position = 1 }];
		owned.DurationMs = 515000;
		KnownTracks(owned);
		Tracklist(Listed(1, "The Grudge", "rec-grudge") with { LengthMs = null });

		await Sync().Run(_album.Id, CancellationToken.None);

		var track = Assert.Single(_savedTracks);
		Assert.Same(owned, track);
		Assert.Equal([new SubsonicId("song-grudge"), new MusicBrainzRecordingId("rec-grudge")], track.ExternalIds);
		Assert.Equal("The Grudge", track.Title);
		Assert.Equal(515000, track.DurationMs);
		Assert.Same(_tool, Assert.Single(track.Artists).Artist);
	}

	[Fact]
	public async Task Matches_a_track_by_recording_id_wherever_it_sits_and_moves_it_to_the_discography_place()
	{
		var owned = Owned(9, "Grudge (album version)", "song-grudge");
		owned.ExternalIds = [new SubsonicId("song-grudge"), new MusicBrainzRecordingId("rec-grudge")];
		KnownTracks(owned);
		Tracklist(Listed(1, "The Grudge", "rec-grudge"));

		await Sync().Run(_album.Id, CancellationToken.None);

		var track = Assert.Single(_savedTracks);
		Assert.Same(owned, track);
		Assert.Equal(1, track.Position);
	}

	[Fact]
	public async Task Replaces_a_different_recording_id_on_a_track_at_the_same_place()
	{
		var owned = Owned(1, "The Grudge", "song-grudge");
		owned.ExternalIds = [new SubsonicId("song-grudge"), new MusicBrainzRecordingId("rec-grudge-merged-away")];
		KnownTracks(owned);
		Tracklist(Listed(1, "The Grudge", "rec-grudge"));

		await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal([new SubsonicId("song-grudge"), new MusicBrainzRecordingId("rec-grudge")], Assert.Single(_savedTracks).ExternalIds);
	}

	[Fact]
	public async Task Adds_the_tracks_the_album_is_missing_without_a_library_id()
	{
		KnownTracks(Owned(1, "The Grudge", "song-grudge"));
		Tracklist(Listed(1, "The Grudge", "rec-grudge"), Listed(2, "Eon Blue Apocalypse", "rec-eon"));

		await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(2, _savedTracks.Count);
		Assert.Equal([new MusicBrainzRecordingId("rec-eon")], _savedTracks[1].ExternalIds);
	}

	[Fact]
	public async Task Keeps_an_unlisted_track_the_library_holds_and_drops_one_only_the_discography_knew()
	{
		var bonus = Owned(14, "Hidden Bonus", "song-bonus");
		var stale = new Track { AlbumId = _album.Id, Title = "Wrongly Listed", DiscNumber = 1, Position = 15, ExternalIds = [new MusicBrainzRecordingId("rec-stale")] };
		var orphan = new Track { AlbumId = _album.Id, Title = "Deleted File", DiscNumber = 1, Position = 16 };
		KnownTracks(bonus, stale, orphan);
		Tracklist(Listed(1, "The Grudge", "rec-grudge"));

		await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(["The Grudge", "Hidden Bonus"], _savedTracks.Select(track => track.Title));
		Assert.Same(bonus, _savedTracks[1]);
	}

	[Fact]
	public async Task Creates_an_artist_credited_only_on_a_track_untracked_and_saves_it_before_the_album()
	{
		Tracklist(Listed(1, "The Grudge", "rec-grudge", DiscographyTool, DiscographyPuscifer));

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		var created = Assert.Single(response.CreatedArtists);
		Assert.Equal("Puscifer", created.Name);
		Assert.False(created.Tracked);
		Assert.Equal([_tool, created], _savedTracks[0].Artists.Select(credit => credit.Artist));
		Assert.Equal([1, 2], _savedTracks[0].Artists.Select(credit => credit.Position));
		Assert.Same(_tool, Assert.Single(response.Album.Artists).Artist);
		Assert.Empty(response.LinkedArtists);
		Assert.Equal([nameof(ICatalogue.SaveArtist), nameof(ICatalogue.SaveAlbum), nameof(ICatalogue.SaveAlbumTracks)], _saves);
	}

	[Fact]
	public async Task Gives_the_discography_artist_id_to_a_track_artist_matching_by_name_instead_of_creating_one()
	{
		var puscifer = new Artist { ExternalIds = [new SubsonicId("art-puscifer")], Name = "PUSCIFER" };
		var owned = Owned(1, "The Grudge", "song-grudge");
		owned.Artists = [new TrackArtist { Artist = _tool, Position = 1 }, new TrackArtist { Artist = puscifer, Position = 2 }];
		KnownTracks(owned);
		_catalogue.Setup(catalogue => catalogue.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([_tool, puscifer]);
		Tracklist(Listed(1, "The Grudge", "rec-grudge", DiscographyTool, DiscographyPuscifer));

		var response = (await Sync().Run(_album.Id, CancellationToken.None)).GetRight;

		Assert.Empty(response.CreatedArtists);
		Assert.Equal([puscifer], response.UpdatedArtists);
		Assert.Equal([new SubsonicId("art-puscifer"), new MusicBrainzArtistId("mb-puscifer")], puscifer.ExternalIds);
		Assert.Equal([_tool, puscifer], _savedTracks[0].Artists.Select(credit => credit.Artist));
	}

	[Fact]
	public async Task Gives_the_merged_album_the_tracks_of_both_synced_with_the_discography()
	{
		var otherEdition = new Album { ExternalIds = [new SubsonicId("alb-lateralus-vinyl"), ReleaseGroupId], Title = "Lateralus", Year = 2001, Tracked = true };
		_knownAlbums.Add(otherEdition);
		KnownTracks(Owned(1, "The Grudge", "song-grudge"));
		var vinylBonus = new Track { AlbumId = otherEdition.Id, Title = "Bonus", DiscNumber = 2, Position = 1, ExternalIds = [new SubsonicId("song-bonus")] };
		_catalogue.Setup(catalogue => catalogue.GetAlbumTracks(otherEdition.Id, It.IsAny<CancellationToken>())).ReturnsAsync([vinylBonus]);
		Tracklist(Listed(1, "The Grudge", "rec-grudge"), Listed(2, "Eon Blue Apocalypse", "rec-eon"));
		IReadOnlyList<Track> merged = [];
		_catalogue.Setup(catalogue => catalogue.MergeAlbums(It.IsAny<Album>(), It.IsAny<AlbumId>(), It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()))
			.Callback((Album _, AlbumId _, IReadOnlyList<Track> tracks, CancellationToken _) => merged = tracks);

		await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(["The Grudge", "Eon Blue Apocalypse", "Bonus"], merged.Select(track => track.Title));
		Assert.All(merged, track => Assert.Equal(_album.Id, track.AlbumId));
		Assert.DoesNotContain(nameof(ICatalogue.SaveAlbumTracks), _saves);
	}

	[Fact]
	public async Task Returns_an_error_when_the_discography_tracks_cannot_be_read()
	{
		_album.Tracked = true;
		_album.ExternalIds = [new SubsonicId("alb-lateralus")];
		_source.Setup(source => source.GetAlbumTracks(ReleaseGroupId, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("rate limited"));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(new UnableToConnectToDiscographyError("rate limited"), Assert.Single(result.GetLeft));
		Assert.Empty(_saves);
	}

	[Fact]
	public async Task Returns_an_error_when_saving_the_tracks_fails()
	{
		Tracklist(Listed(1, "The Grudge", "rec-grudge"));
		_catalogue.Setup(catalogue => catalogue.SaveAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<IReadOnlyList<Track>>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("database is locked"));

		var result = await Sync().Run(_album.Id, CancellationToken.None);

		Assert.Equal(new UnableToSaveAlbumError("database is locked"), Assert.Single(result.GetLeft));
	}

	private void Tracklist(params DiscographyTrack[] tracks)
	{
		_album.Tracked = true;
		_resolver.Setup(resolver => resolver.Resolve(_album, It.IsAny<CancellationToken>()))
			.ReturnsAsync(Discography(DiscographyTool) with { Tracks = tracks });
	}

	private void KnownTracks(params Track[] tracks)
	{
		_catalogue.Setup(catalogue => catalogue.GetAlbumTracks(_album.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tracks);
	}

	private Track Owned(int position, string title, string subsonicId)
	{
		return new Track { AlbumId = _album.Id, Title = title, DiscNumber = 1, Position = position, ExternalIds = [new SubsonicId(subsonicId)] };
	}

	private static DiscographyTrack Listed(int position, string title, string recordingId, params DiscographyArtist[] artists)
	{
		return new DiscographyTrack(new MusicBrainzRecordingId(recordingId), title, 1, position, position * 1000, artists.Length == 0 ? [DiscographyTool] : artists);
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
