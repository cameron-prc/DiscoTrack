using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.ExternalIds;
using DiscoTrack.Library;
using DiscoTrack.Library.SyncLibrary;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static DiscoTrack.Tests.Library.LibraryValues;

namespace DiscoTrack.Tests.Library;

public class SyncLibraryTests
{
	private static readonly LibraryArtist Tool = new(new SubsonicId("art-tool"), "Tool", new MusicBrainzId("mb-tool"));
	private static readonly LibraryArtist Shihad = new(new SubsonicId("art-shihad"), "Shihad", new MusicBrainzId("mb-shihad"));

	private readonly FakeCatalogue _catalogue = new();
	private readonly FakeLibrarySource _source = new();

	private SyncLibrary Sync()
	{
		return new SyncLibrary(_catalogue, _source, NullLogger<SyncLibrary>.Instance);
	}

	[Fact]
	public async Task Empty_library_into_empty_catalogue_adds_nothing()
	{
		await Sync().Run(CancellationToken.None);

		Assert.Empty(_catalogue.Artists);
		Assert.Empty(_catalogue.Albums);
	}

	[Fact]
	public async Task Adds_every_album_and_each_distinct_artist_once()
	{
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-alice", "Alice by the Moon", 2008, TheChecks),
			Album("alb-odelay", "Odelay", 1996, Beck),
			Album("alb-capitale", "Capitale", 2021, McBaise),
		]);

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("art-checks"), new SubsonicId("art-beck"), new SubsonicId("art-mcbaise")], _catalogue.Artists.SelectMany(a => a.ExternalIds));
		Assert.Equal([new SubsonicId("alb-hunting-whales"), new SubsonicId("alb-alice"), new SubsonicId("alb-odelay"), new SubsonicId("alb-capitale")], _catalogue.Albums.SelectMany(a => a.ExternalIds));
	}

	[Fact]
	public async Task Maps_album_fields_and_one_based_credit_positions()
	{
		_source.Albums.Add(Album("alb-split", "Split", 2010, TheChecks, Beck));

		await Sync().Run(CancellationToken.None);

		var album = Assert.Single(_catalogue.Albums);
		Assert.Equal([new SubsonicId("alb-split")], album.ExternalIds);
		Assert.Equal("Split", album.Title);
		Assert.Equal(2010, album.Year);
		Assert.False(album.IsCompilation);
		Assert.Equal([1, 2], album.Artists.Select(a => a.Position));
		Assert.Equal(["The Checks", "Beck"], album.Artists.Select(a => a.Artist.Name));
	}

	[Fact]
	public async Task Credits_point_at_the_artists_that_were_added()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Sync().Run(CancellationToken.None);

		var addedArtist = Assert.Single(_catalogue.Artists);
		var credit = Assert.Single(Assert.Single(_catalogue.Albums).Artists);
		Assert.Same(addedArtist, credit.Artist);
	}

	[Fact]
	public async Task Credits_reuse_an_artist_already_in_the_catalogue()
	{
		var existingChecks = new Artist { ExternalIds = [new SubsonicId("art-checks")], Name = "The Checks" };
		_catalogue.Artists.Add(existingChecks);
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Sync().Run(CancellationToken.None);

		Assert.Equal([existingChecks.Id], _catalogue.Artists.Select(a => a.Id));
		var credit = Assert.Single(Assert.Single(_catalogue.Albums).Artists);
		Assert.Same(existingChecks, credit.Artist);
	}

	[Fact]
	public async Task Skips_albums_already_in_the_catalogue()
	{
		_catalogue.Artists.Add(new Artist { ExternalIds = [new SubsonicId("art-checks")], Name = "The Checks" });
		_catalogue.Albums.Add(new Album { ExternalIds = [new SubsonicId("alb-hunting-whales")], Title = "Hunting Whales", Year = 2007 });
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-alice", "Alice by the Moon", 2008, TheChecks),
		]);

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("alb-hunting-whales"), new SubsonicId("alb-alice")], _catalogue.Albums.SelectMany(a => a.ExternalIds));
		Assert.Single(_catalogue.Artists);
	}

	[Fact]
	public async Task A_second_run_over_the_same_library_adds_nothing()
	{
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-split", "Split", 2010, TheChecks, Beck),
		]);
		await Sync().Run(CancellationToken.None);
		var artistCount = _catalogue.Artists.Count;
		var albumCount = _catalogue.Albums.Count;

		await Sync().Run(CancellationToken.None);

		Assert.Equal(artistCount, _catalogue.Artists.Count);
		Assert.Equal(albumCount, _catalogue.Albums.Count);
	}

	[Fact]
	public async Task Skips_an_album_with_no_year_and_keeps_the_rest()
	{
		_source.Albums.AddRange([
			Album("alb-untagged", "Untagged", null, TheChecks),
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
		]);

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("alb-hunting-whales")], _catalogue.Albums.SelectMany(a => a.ExternalIds));
		Assert.Single(_catalogue.Artists);
	}

	[Fact]
	public async Task Stores_musicbrainz_ids_alongside_the_source_ids()
	{
		var tool = new LibraryArtist(new SubsonicId("art-tool"), "Tool", new MusicBrainzId("mb-tool"));
		_source.Albums.Add(Album("alb-lateralus", "Lateralus", 2001, tool) with { MusicBrainzId = new MusicBrainzId("mb-lateralus") });

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzId("mb-tool")], Assert.Single(_catalogue.Artists).ExternalIds);
		Assert.Equal([new SubsonicId("alb-lateralus"), new MusicBrainzId("mb-lateralus")], Assert.Single(_catalogue.Albums).ExternalIds);
	}

	[Fact]
	public async Task Artists_sharing_a_musicbrainz_id_in_the_run_become_one_artist_credited_on_each_album()
	{
		var wolfAlice = new LibraryArtist(new SubsonicId("art-wolf-alice"), "Wolf Alice", new MusicBrainzId("mb-wolf-alice"));
		var wolfAlce = new LibraryArtist(new SubsonicId("art-wolf-alce"), "Wolf Alce", new MusicBrainzId("mb-wolf-alice"));
		_source.Albums.AddRange([
			Album("alb-my-love-is-cool", "My Love Is Cool", 2015, wolfAlice),
			Album("alb-visions", "Visions of a Life", 2017, wolfAlce),
		]);

		await Sync().Run(CancellationToken.None);

		var artist = Assert.Single(_catalogue.Artists);
		Assert.Equal("Wolf Alice", artist.Name);
		Assert.Equal([new SubsonicId("art-wolf-alice"), new MusicBrainzId("mb-wolf-alice"), new SubsonicId("art-wolf-alce")], artist.ExternalIds);
		Assert.Equal(["My Love Is Cool", "Visions of a Life"], _catalogue.Albums.Select(a => a.Title));
		Assert.All(_catalogue.Albums, album => Assert.Same(artist, Assert.Single(album.Artists).Artist));
	}

	[Fact]
	public async Task A_second_run_over_artists_and_albums_sharing_a_musicbrainz_id_saves_nothing()
	{
		var wolfAlice = new LibraryArtist(new SubsonicId("art-wolf-alice"), "Wolf Alice", new MusicBrainzId("mb-wolf-alice"));
		var wolfAlce = new LibraryArtist(new SubsonicId("art-wolf-alce"), "Wolf Alce", new MusicBrainzId("mb-wolf-alice"));
		_source.Albums.AddRange([
			Album("alb-visions", "Visions of a Life", 2017, wolfAlice) with { MusicBrainzId = new MusicBrainzId("mb-visions") },
			Album("alb-visions-rip", "Visions of a Life", 2017, wolfAlce) with { MusicBrainzId = new MusicBrainzId("mb-visions") },
		]);
		await Sync().Run(CancellationToken.None);
		_catalogue.Calls.Clear();

		var result = await Sync().Run(CancellationToken.None);

		Assert.Empty(result.GetRight.Artists);
		Assert.Empty(result.GetRight.Albums);
		Assert.Equal([nameof(FakeCatalogue.GetArtists), nameof(FakeCatalogue.GetAlbums)], _catalogue.Calls);
		Assert.Equal(
			[new SubsonicId("art-wolf-alice"), new MusicBrainzId("mb-wolf-alice"), new SubsonicId("art-wolf-alce")],
			Assert.Single(_catalogue.Artists).ExternalIds);
		Assert.Equal(
			[new SubsonicId("alb-visions"), new MusicBrainzId("mb-visions"), new SubsonicId("alb-visions-rip")],
			Assert.Single(_catalogue.Albums).ExternalIds);
	}

	[Fact]
	public async Task Adds_a_duplicate_library_id_to_the_catalogue_entity_without_dropping_the_one_it_has()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzId("mb-tool")], Name = "Tool" };
		var lateralus = new Album { ExternalIds = [new SubsonicId("alb-lateralus"), new MusicBrainzId("mb-lateralus")], Title = "Lateralus", Year = 2001 };
		_catalogue.Artists.Add(tool);
		_catalogue.Albums.Add(lateralus);
		var toolAgain = Tool with { Id = new SubsonicId("art-tool-again") };
		_source.Albums.AddRange([
			Album("alb-lateralus", "Lateralus", 2001, Tool) with { MusicBrainzId = new MusicBrainzId("mb-lateralus") },
			Album("alb-lateralus-rip", "Lateralus", 2001, toolAgain) with { MusicBrainzId = new MusicBrainzId("mb-lateralus") },
		]);

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal([tool], result.GetRight.Artists);
		Assert.Equal([lateralus], result.GetRight.Albums);
		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzId("mb-tool"), new SubsonicId("art-tool-again")], tool.ExternalIds);
		Assert.Equal([new SubsonicId("alb-lateralus"), new MusicBrainzId("mb-lateralus"), new SubsonicId("alb-lateralus-rip")], lateralus.ExternalIds);
	}

	[Fact]
	public async Task Saves_only_the_catalogue_entities_whose_ids_changed()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzId("mb-tool")], Name = "Tool" };
		var shihad = new Artist { ExternalIds = [new SubsonicId("art-shihad-old"), new MusicBrainzId("mb-shihad")], Name = "Shihad" };
		_catalogue.Artists.AddRange([tool, shihad]);
		_catalogue.Albums.Add(new Album { ExternalIds = [new SubsonicId("alb-lateralus")], Title = "Lateralus", Year = 2001 });
		_catalogue.Albums.Add(new Album { ExternalIds = [new SubsonicId("alb-killjoy")], Title = "Killjoy", Year = 1995 });
		_source.Albums.AddRange([
			Album("alb-lateralus", "Lateralus", 2001, Tool),
			Album("alb-killjoy", "Killjoy", 1995, Shihad),
		]);

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal([shihad], result.GetRight.Artists);
		Assert.Empty(result.GetRight.Albums);
		Assert.Equal([new MusicBrainzId("mb-shihad"), new SubsonicId("art-shihad")], shihad.ExternalIds);
		Assert.Single(_catalogue.Calls, call => call == nameof(FakeCatalogue.SaveArtist));
		Assert.DoesNotContain(nameof(FakeCatalogue.SaveAlbum), _catalogue.Calls);
	}

	[Fact]
	public async Task Links_a_library_album_to_the_catalogue_album_holding_its_musicbrainz_id()
	{
		var odelay = new Album { ExternalIds = [new SubsonicId("alb-odelay"), new MusicBrainzId("mb-odelay")], Title = "Odelay", Year = 1996 };
		_catalogue.Albums.Add(odelay);
		_source.Albums.Add(Album("alb-odelay-rip", "Odelay", 1996, Beck) with { MusicBrainzId = new MusicBrainzId("mb-odelay") });

		await Sync().Run(CancellationToken.None);

		Assert.Same(odelay, Assert.Single(_catalogue.Albums));
		Assert.Equal([new MusicBrainzId("mb-odelay"), new SubsonicId("alb-odelay-rip")], odelay.ExternalIds);
	}

	[Fact]
	public async Task Removes_the_library_ids_of_artists_and_albums_no_longer_in_the_library_and_keeps_them_in_the_catalogue()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzId("mb-tool")], Name = "Tool" };
		var lateralus = new Album { ExternalIds = [new SubsonicId("alb-lateralus"), new MusicBrainzId("mb-lateralus")], Title = "Lateralus", Year = 2001 };
		var checks = new Artist { ExternalIds = [new SubsonicId("art-checks")], Name = "The Checks" };
		var huntingWhales = new Album { ExternalIds = [new SubsonicId("alb-hunting-whales")], Title = "Hunting Whales", Year = 2007 };
		_catalogue.Artists.AddRange([tool, checks]);
		_catalogue.Albums.AddRange([lateralus, huntingWhales]);
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal([tool], result.GetRight.Artists);
		Assert.Equal([lateralus], result.GetRight.Albums);
		Assert.Equal([new MusicBrainzId("mb-tool")], tool.ExternalIds);
		Assert.Equal([new MusicBrainzId("mb-lateralus")], lateralus.ExternalIds);
		Assert.Equal([tool, checks], _catalogue.Artists);
		Assert.Equal([lateralus, huntingWhales], _catalogue.Albums);
	}

	[Fact]
	public async Task Leaves_catalogue_entities_that_hold_no_library_id_alone()
	{
		_catalogue.Artists.Add(new Artist { ExternalIds = [new MusicBrainzId("mb-tool")], Name = "Tool" });
		_catalogue.Albums.Add(new Album { ExternalIds = [new MusicBrainzId("mb-lateralus")], Title = "Lateralus", Year = 2001 });
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal(["The Checks"], result.GetRight.Artists.Select(a => a.Name));
		Assert.Equal(["Hunting Whales"], result.GetRight.Albums.Select(a => a.Title));
	}

	[Fact]
	public async Task A_second_run_after_removing_library_ids_saves_nothing()
	{
		_catalogue.Artists.Add(new Artist { ExternalIds = [new SubsonicId("art-tool")], Name = "Tool" });
		_catalogue.Albums.Add(new Album { ExternalIds = [new SubsonicId("alb-lateralus")], Title = "Lateralus", Year = 2001 });
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		await Sync().Run(CancellationToken.None);
		_catalogue.Calls.Clear();

		await Sync().Run(CancellationToken.None);

		Assert.Equal([nameof(FakeCatalogue.GetArtists), nameof(FakeCatalogue.GetAlbums)], _catalogue.Calls);
	}

	[Fact]
	public async Task An_empty_library_removes_every_library_id()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzId("mb-tool")], Name = "Tool" };
		var lateralus = new Album { ExternalIds = [new SubsonicId("alb-lateralus")], Title = "Lateralus", Year = 2001 };
		_catalogue.Artists.Add(tool);
		_catalogue.Albums.Add(lateralus);

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal([tool], result.GetRight.Artists);
		Assert.Equal([lateralus], result.GetRight.Albums);
		Assert.Equal([new MusicBrainzId("mb-tool")], tool.ExternalIds);
		Assert.Empty(lateralus.ExternalIds);
	}

	[Fact]
	public async Task Saves_artists_before_albums()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Sync().Run(CancellationToken.None);

		Assert.Equal(
			[nameof(FakeCatalogue.GetArtists), nameof(FakeCatalogue.GetAlbums), nameof(FakeCatalogue.SaveArtist), nameof(FakeCatalogue.SaveAlbum)],
			_catalogue.Calls);
	}

	[Fact]
	public async Task Returns_a_read_catalogue_error_when_reading_artists_fails_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.GetArtists);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToReadCatalogueError>(Assert.Single(result.GetLeft));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Returns_a_read_catalogue_error_when_reading_albums_fails_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.GetAlbums);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToReadCatalogueError>(Assert.Single(result.GetLeft));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Returns_a_read_library_error_when_reading_the_library_fails_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_source.FailingMethod = nameof(FakeLibrarySource.GetAlbums);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToReadLibraryError>(Assert.Single(result.GetLeft));

		AssertNothingWritten();
	}

	[Fact]
	public async Task A_failed_artist_save_is_reported_and_the_other_artists_are_still_saved()
	{
		var catalogue = new Mock<ICatalogue>();
		catalogue.Setup(c => c.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue
			.Setup(c => c.SaveArtist(It.Is<Artist>(artist => artist.Name == "The Checks"), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("database is locked"));
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-odelay", "Odelay", 1996, Beck),
		]);

		var result = await new SyncLibrary(catalogue.Object, _source, NullLogger<SyncLibrary>.Instance).Run(CancellationToken.None);

		var response = result.GetRight;
		var failure = Assert.Single(response.Failures);
		Assert.Equal(SyncAction.Save, failure.Action);
		Assert.Equal("The Checks", Assert.IsType<Artist>(failure.Entity).Name);
		Assert.Equal("database is locked", failure.Reason);
		Assert.Equal(["Beck"], response.Artists.Select(a => a.Name));
	}

	[Fact]
	public async Task A_failed_album_save_is_reported_and_the_other_albums_are_still_saved()
	{
		var catalogue = new Mock<ICatalogue>();
		catalogue.Setup(c => c.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue
			.Setup(c => c.SaveAlbum(It.Is<Album>(album => album.Title == "Hunting Whales"), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("database is locked"));
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-odelay", "Odelay", 1996, Beck),
		]);

		var result = await new SyncLibrary(catalogue.Object, _source, NullLogger<SyncLibrary>.Instance).Run(CancellationToken.None);

		var response = result.GetRight;
		var failure = Assert.Single(response.Failures);
		Assert.Equal(SyncAction.Save, failure.Action);
		Assert.Equal("Hunting Whales", Assert.IsType<Album>(failure.Entity).Title);
		Assert.Equal("database is locked", failure.Reason);
		Assert.Equal(["Odelay"], response.Albums.Select(a => a.Title));
		Assert.Equal(["The Checks", "Beck"], response.Artists.Select(a => a.Name));
	}

	[Fact]
	public async Task Cancellation_during_a_save_propagates_instead_of_becoming_a_failure()
	{
		var catalogue = new Mock<ICatalogue>();
		catalogue.Setup(c => c.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue
			.Setup(c => c.SaveArtist(It.IsAny<Artist>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Assert.ThrowsAsync<OperationCanceledException>(() => new SyncLibrary(catalogue.Object, _source, NullLogger<SyncLibrary>.Instance).Run(CancellationToken.None));
	}

	[Fact]
	public async Task Saves_and_returns_the_artists_and_albums_it_linked()
	{
		var tool = new Artist { ExternalIds = [new MusicBrainzId("mb-tool")], Name = "Tool" };
		var lateralus = new Album { ExternalIds = [new MusicBrainzId("mb-lateralus")], Title = "Lateralus", Year = 2001 };
		_catalogue.Artists.Add(tool);
		_catalogue.Albums.Add(lateralus);
		_source.Albums.Add(Album("alb-lateralus", "Lateralus", 2001, Tool) with { MusicBrainzId = new MusicBrainzId("mb-lateralus") });

		var result = await Sync().Run(CancellationToken.None);

		var response = result.GetRight;
		Assert.Equal([tool], response.Artists);
		Assert.Equal([lateralus], response.Albums);
		Assert.Equal([new MusicBrainzId("mb-tool"), new SubsonicId("art-tool")], tool.ExternalIds);
		Assert.Equal([new MusicBrainzId("mb-lateralus"), new SubsonicId("alb-lateralus")], lateralus.ExternalIds);
		Assert.Empty(response.Failures);
		Assert.Contains(nameof(FakeCatalogue.SaveArtist), _catalogue.Calls);
		Assert.Contains(nameof(FakeCatalogue.SaveAlbum), _catalogue.Calls);
	}

	[Fact]
	public async Task Saves_an_artist_once_when_it_is_added_and_linked_in_the_same_run()
	{
		var toolAgain = Tool with { Id = new SubsonicId("art-tool-again") };
		_source.Albums.AddRange([
			Album("alb-lateralus", "Lateralus", 2001, Tool),
			Album("alb-aenima", "Aenima", 1996, toolAgain),
		]);

		var result = await Sync().Run(CancellationToken.None);

		Assert.Single(result.GetRight.Artists);
		Assert.Single(_catalogue.Calls, call => call == nameof(FakeCatalogue.SaveArtist));
	}

	[Fact]
	public async Task Returns_the_added_artists_and_albums_and_the_albums_it_could_not_import()
	{
		_source.Albums.AddRange([
			Album("alb-untagged", "Untagged", null, TheChecks),
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
		]);

		var result = await Sync().Run(CancellationToken.None);

		var response = result.GetRight;
		Assert.Equal([new SubsonicId("art-checks")], response.Artists.SelectMany(a => a.ExternalIds));
		Assert.Equal([new SubsonicId("alb-hunting-whales")], response.Albums.SelectMany(a => a.ExternalIds));
		var failure = Assert.Single(response.Failures);
		Assert.Equal(SyncAction.Import, failure.Action);
		Assert.Equal(new SubsonicId("alb-untagged"), Assert.IsType<LibraryAlbum>(failure.Entity).Id);
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_becoming_an_error()
	{
		var source = new Mock<ILibrarySource>();
		source
			.Setup(s => s.GetAlbums(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => new SyncLibrary(_catalogue, source.Object, NullLogger<SyncLibrary>.Instance).Run(CancellationToken.None));

		AssertNothingWritten();
	}

	private void AssertNothingWritten()
	{
		Assert.DoesNotContain(nameof(FakeCatalogue.SaveArtist), _catalogue.Calls);
		Assert.DoesNotContain(nameof(FakeCatalogue.SaveAlbum), _catalogue.Calls);
		Assert.Empty(_catalogue.Artists);
		Assert.Empty(_catalogue.Albums);
	}
}
