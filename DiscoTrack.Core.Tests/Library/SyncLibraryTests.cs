using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;
using DiscoTrack.Core.Library;
using DiscoTrack.Core.Library.SyncLibrary;
using Moq;
using static DiscoTrack.Core.Tests.Library.LibraryValues;

namespace DiscoTrack.Core.Tests.Library;

public class SyncLibraryTests
{
	private readonly FakeCatalogue _catalogue = new();
	private readonly FakeLibrarySource _source = new();

	private SyncLibrary Sync()
	{
		return new SyncLibrary(_catalogue, _source);
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

		Assert.Equal(["art-checks", "art-beck", "art-mcbaise"], _catalogue.Artists.Select(a => a.SubsonicId));
		Assert.Equal(["alb-hunting-whales", "alb-alice", "alb-odelay", "alb-capitale"], _catalogue.Albums.Select(a => a.SubsonicId));
	}

	[Fact]
	public async Task Maps_album_fields_and_one_based_credit_positions()
	{
		_source.Albums.Add(Album("alb-split", "Split", 2010, TheChecks, Beck));

		await Sync().Run(CancellationToken.None);

		var album = Assert.Single(_catalogue.Albums);
		Assert.Equal("alb-split", album.SubsonicId);
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
		var existingChecks = new Artist { SubsonicId = "art-checks", Name = "The Checks" };
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
		_catalogue.Artists.Add(new Artist { SubsonicId = "art-checks", Name = "The Checks" });
		_catalogue.Albums.Add(new Album { SubsonicId = "alb-hunting-whales", Title = "Hunting Whales", Year = 2007 });
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-alice", "Alice by the Moon", 2008, TheChecks),
		]);

		await Sync().Run(CancellationToken.None);

		Assert.Equal(["alb-hunting-whales", "alb-alice"], _catalogue.Albums.Select(a => a.SubsonicId));
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

		Assert.Equal(["alb-hunting-whales"], _catalogue.Albums.Select(a => a.SubsonicId));
		Assert.Single(_catalogue.Artists);
	}

	[Fact]
	public async Task Adds_artists_before_albums()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Sync().Run(CancellationToken.None);

		Assert.Equal(
			[nameof(FakeCatalogue.GetArtists), nameof(FakeCatalogue.GetAlbums), nameof(FakeCatalogue.AddArtists), nameof(FakeCatalogue.AddAlbums)],
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
	public async Task Returns_a_save_error_when_adding_artists_fails_and_does_not_add_albums()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.AddArtists);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToSaveCatalogueError>(Assert.Single(result.GetLeft));

		Assert.DoesNotContain(nameof(FakeCatalogue.AddAlbums), _catalogue.Calls);
	}

	[Fact]
	public async Task Returns_a_save_error_when_adding_albums_fails_after_artists_were_added()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.AddAlbums);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToSaveCatalogueError>(Assert.Single(result.GetLeft));

		Assert.Single(_catalogue.Artists);
		Assert.Empty(_catalogue.Albums);
	}

	[Fact]
	public async Task Returns_the_new_artists_and_albums_and_the_skipped_albums()
	{
		_source.Albums.AddRange([
			Album("alb-untagged", "Untagged", null, TheChecks),
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
		]);

		var result = await Sync().Run(CancellationToken.None);

		var response = result.GetRight;
		Assert.Equal(["art-checks"], response.NewArtists.Select(a => a.SubsonicId));
		Assert.Equal(["alb-hunting-whales"], response.NewAlbums.Select(a => a.SubsonicId));
		Assert.Equal(["alb-untagged"], response.UnprocessedAlbums.Select(a => a.SubsonicId));
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_becoming_an_error()
	{
		var source = new Mock<ILibrarySource>();
		source
			.Setup(s => s.GetAlbums(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => new SyncLibrary(_catalogue, source.Object).Run(CancellationToken.None));

		AssertNothingWritten();
	}

	private void AssertNothingWritten()
	{
		Assert.DoesNotContain(nameof(FakeCatalogue.AddArtists), _catalogue.Calls);
		Assert.DoesNotContain(nameof(FakeCatalogue.AddAlbums), _catalogue.Calls);
		Assert.Empty(_catalogue.Artists);
		Assert.Empty(_catalogue.Albums);
	}
}
