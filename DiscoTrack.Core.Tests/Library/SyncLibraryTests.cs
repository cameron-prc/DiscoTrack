using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;
using DiscoTrack.Core.Library;
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
	public async Task Propagates_a_failure_reading_catalogue_artists_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.GetArtists);

		await Assert.ThrowsAsync<InvalidOperationException>(() => Sync().Run(CancellationToken.None));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Propagates_a_failure_reading_catalogue_albums_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.GetAlbums);

		await Assert.ThrowsAsync<InvalidOperationException>(() => Sync().Run(CancellationToken.None));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Propagates_a_failure_reading_the_library_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_source.FailingMethod = nameof(FakeLibrarySource.GetAlbums);

		await Assert.ThrowsAsync<InvalidOperationException>(() => Sync().Run(CancellationToken.None));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Propagates_a_failure_adding_artists_and_does_not_add_albums()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.AddArtists);

		await Assert.ThrowsAsync<InvalidOperationException>(() => Sync().Run(CancellationToken.None));

		Assert.DoesNotContain(nameof(FakeCatalogue.AddAlbums), _catalogue.Calls);
	}

	[Fact]
	public async Task Propagates_a_failure_adding_albums_after_artists_were_added()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.AddAlbums);

		await Assert.ThrowsAsync<InvalidOperationException>(() => Sync().Run(CancellationToken.None));

		Assert.Single(_catalogue.Artists);
		Assert.Empty(_catalogue.Albums);
	}

	private void AssertNothingWritten()
	{
		Assert.DoesNotContain(nameof(FakeCatalogue.AddArtists), _catalogue.Calls);
		Assert.DoesNotContain(nameof(FakeCatalogue.AddAlbums), _catalogue.Calls);
		Assert.Empty(_catalogue.Artists);
		Assert.Empty(_catalogue.Albums);
	}
}
