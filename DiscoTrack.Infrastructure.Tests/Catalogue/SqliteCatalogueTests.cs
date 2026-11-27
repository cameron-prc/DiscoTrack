using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Catalogue;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Sqlite.Migrations;
using DiscoTrack.Infrastructure.Tests.Sqlite;
using DiscoTrack.Tracks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tests.Catalogue;

public sealed class SqliteCatalogueTests : IDisposable
{
	private readonly TestDatabase _db = new();
	private readonly SqliteCatalogue _catalogue;

	public SqliteCatalogueTests()
	{
		Migrator.Migrate(_db.ConnectionString);
		_catalogue = new SqliteCatalogue(Options.Create(new SqliteOptions { ConnectionString = _db.ConnectionString }));
	}

	public void Dispose()
	{
		_db.Dispose();
	}

	[Fact]
	public async Task A_fresh_database_is_empty()
	{
		Assert.Empty(await _catalogue.GetArtists(CancellationToken.None));
		Assert.Empty(await _catalogue.GetAlbums(CancellationToken.None));
	}

	[Fact]
	public async Task GetArtists_returns_added_artists_ordered_by_name()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await SaveArtists(tool, puscifer);

		var artists = await _catalogue.GetArtists(CancellationToken.None);

		Assert.Equal(["Puscifer", "Tool"], artists.Select(a => a.Name));
		Assert.Equal([new SubsonicId("art-puscifer"), new SubsonicId("art-tool")], artists.SelectMany(a => a.ExternalIds));
		Assert.Equal([puscifer.Id, tool.Id], artists.Select(a => a.Id));
	}

	[Fact]
	public async Task SaveArtist_saves_an_artist_without_external_ids()
	{
		await SaveArtists(new Artist { Name = "Shihad" });

		var artist = Assert.Single(await _catalogue.GetArtists(CancellationToken.None));
		Assert.Empty(artist.ExternalIds);
		Assert.Equal("Shihad", artist.Name);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task SaveArtist_round_trips_the_tracked_flag_on_the_artist_and_its_album_credits(bool tracked)
	{
		var tool = Artist("art-tool", "Tool");
		tool.Tracked = tracked;
		await SaveArtists(tool);
		var lateralus = Album("alb-l", "Lateralus", 2001, Credit(tool, 1));
		await SaveAlbums(lateralus);

		Assert.Equal(tracked, Assert.Single(await _catalogue.GetArtists(CancellationToken.None)).Tracked);
		Assert.Equal(tracked, (await _catalogue.GetArtist(tool.Id, CancellationToken.None))!.Tracked);
		Assert.Equal(tracked, Assert.Single(await _catalogue.GetAlbumArtists(lateralus.Id, CancellationToken.None)).Tracked);
		Assert.Equal(tracked, Assert.Single(await _catalogue.GetAlbums(CancellationToken.None)).Artists[0].Artist.Tracked);
		Assert.Equal(tracked, (await _catalogue.GetAlbum(lateralus.Id, CancellationToken.None))!.Artists[0].Artist.Tracked);
		Assert.Equal(tracked, Assert.Single(await _catalogue.GetArtistAlbums(tool.Id, CancellationToken.None)).Artists[0].Artist.Tracked);
	}

	[Fact]
	public async Task SaveArtist_round_trips_the_last_scanned_time_of_each_provider_on_the_artist_and_its_album_credits()
	{
		var scannedAt = new Dictionary<Provider, DateTimeOffset>
		{
			[Provider.MusicBrainzArtist] = new(2026, 11, 26, 9, 30, 15, 123, TimeSpan.Zero),
			[Provider.Subsonic] = new(2026, 11, 20, 18, 0, 0, TimeSpan.Zero),
		};
		var tool = Artist("art-tool", "Tool");
		tool.LastScannedAt = scannedAt;
		await SaveArtists(tool);
		var lateralus = Album("alb-l", "Lateralus", 2001, Credit(tool, 1));
		await SaveAlbums(lateralus);

		Assert.Equal(scannedAt, Assert.Single(await _catalogue.GetArtists(CancellationToken.None)).LastScannedAt);
		Assert.Equal(scannedAt, (await _catalogue.GetArtist(tool.Id, CancellationToken.None))!.LastScannedAt);
		Assert.Equal(scannedAt, Assert.Single(await _catalogue.GetAlbumArtists(lateralus.Id, CancellationToken.None)).LastScannedAt);
		Assert.Equal(scannedAt, Assert.Single(await _catalogue.GetAlbums(CancellationToken.None)).Artists[0].Artist.LastScannedAt);
		Assert.Equal(scannedAt, (await _catalogue.GetAlbum(lateralus.Id, CancellationToken.None))!.Artists[0].Artist.LastScannedAt);
		Assert.Equal(scannedAt, Assert.Single(await _catalogue.GetArtistAlbums(tool.Id, CancellationToken.None)).Artists[0].Artist.LastScannedAt);
	}

	[Fact]
	public async Task SaveArtist_saves_an_artist_that_was_never_scanned()
	{
		await SaveArtists(Artist("art-tool", "Tool"));

		Assert.Empty(Assert.Single(await _catalogue.GetArtists(CancellationToken.None)).LastScannedAt);
	}

	[Fact]
	public async Task SaveArtist_replaces_the_last_scanned_times_of_an_existing_artist()
	{
		var tool = Artist("art-tool", "Tool");
		tool.SetLastScannedAt(Provider.Subsonic, new DateTimeOffset(2026, 11, 20, 18, 0, 0, TimeSpan.Zero));
		await SaveArtists(tool);

		var rescannedAt = new DateTimeOffset(2026, 11, 26, 9, 30, 0, TimeSpan.Zero);
		tool.LastScannedAt = new Dictionary<Provider, DateTimeOffset> { [Provider.MusicBrainzArtist] = rescannedAt };
		await SaveArtists(tool);

		Assert.Equal([KeyValuePair.Create(Provider.MusicBrainzArtist, rescannedAt)], Assert.Single(await _catalogue.GetArtists(CancellationToken.None)).LastScannedAt);
	}

	[Fact]
	public async Task MergeArtists_keeps_the_survivors_last_scanned_times_and_drops_the_absorbed_artists()
	{
		var scannedAt = new DateTimeOffset(2026, 11, 26, 9, 30, 0, TimeSpan.Zero);
		var library = Artist("art-tool", "Tool");
		var credited = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		credited.SetLastScannedAt(Provider.MusicBrainzArtist, scannedAt.AddDays(-7));
		await SaveArtists(library, credited);

		library.ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")];
		library.SetLastScannedAt(Provider.MusicBrainzArtist, scannedAt);
		await _catalogue.MergeArtists(library, credited.Id, CancellationToken.None);

		Assert.Equal([KeyValuePair.Create(Provider.MusicBrainzArtist, scannedAt)], Assert.Single(await _catalogue.GetArtists(CancellationToken.None)).LastScannedAt);
	}

	[Fact]
	public async Task SaveArtist_updates_the_tracked_flag_of_an_existing_artist()
	{
		var tool = Artist("art-tool", "Tool");
		await SaveArtists(tool);

		tool.Tracked = true;
		await SaveArtists(tool);

		Assert.True(Assert.Single(await _catalogue.GetArtists(CancellationToken.None)).Tracked);
	}

	[Fact]
	public async Task SaveArtist_rejects_a_different_artist_with_the_same_discography_id()
	{
		await SaveArtists(new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool" });

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.SaveArtist(new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool again" }, CancellationToken.None));

		Assert.Equal(["Tool"], (await _catalogue.GetArtists(CancellationToken.None)).Select(a => a.Name));
	}

	[Fact]
	public async Task SaveArtist_lets_two_artists_hold_the_same_library_id()
	{
		var house = new Artist { ExternalIds = [new SubsonicId("art-pendulum"), new MusicBrainzArtistId("mb-pendulum-house")], Name = "Pendulum", Disambiguation = "Australian 1990s progressive house artist" };
		var drumAndBass = new Artist { ExternalIds = [new SubsonicId("art-pendulum"), new MusicBrainzArtistId("mb-pendulum-dnb")], Name = "Pendulum" };

		await SaveArtists(house, drumAndBass);

		var artists = await _catalogue.GetArtists(CancellationToken.None);
		Assert.Equal(2, artists.Count);
		Assert.All(artists, artist => Assert.Contains(new SubsonicId("art-pendulum"), artist.ExternalIds));
		Assert.Equal(["Pendulum", "Pendulum (Australian 1990s progressive house artist)"], artists.Select(artist => artist.DisplayName).Order());
	}

	[Fact]
	public async Task Artists_round_trip_their_disambiguation_on_every_read()
	{
		var tool = Artist("art-tool", "Tool");
		tool.Disambiguation = "American rock band";
		var lateralus = Album("alb-l", "Lateralus", 2001, Credit(tool, 1));
		await SaveArtists(tool);
		await SaveAlbums(lateralus);
		await _catalogue.SaveAlbumTracks(lateralus.Id, [Track(lateralus, 1, 1, "The Grudge", TrackCredit(tool, 1))], CancellationToken.None);

		Assert.Equal("American rock band", Assert.Single(await _catalogue.GetArtists(CancellationToken.None)).Disambiguation);
		Assert.Equal("American rock band", (await _catalogue.GetArtist(tool.Id, CancellationToken.None))!.Disambiguation);
		Assert.Equal("American rock band", Assert.Single(await _catalogue.GetAlbumArtists(lateralus.Id, CancellationToken.None)).Disambiguation);
		Assert.Equal("American rock band", Assert.Single(await _catalogue.GetAlbums(CancellationToken.None)).Artists[0].Artist.Disambiguation);
		Assert.Equal("American rock band", (await _catalogue.GetAlbum(lateralus.Id, CancellationToken.None))!.Artists[0].Artist.Disambiguation);
		Assert.Equal("American rock band", Assert.Single(await _catalogue.GetArtistAlbums(tool.Id, CancellationToken.None)).Artists[0].Artist.Disambiguation);
		Assert.Equal("American rock band", Assert.Single(await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None)).Artists[0].Artist.Disambiguation);

		tool.Disambiguation = null;
		await SaveArtists(tool);

		Assert.Null(Assert.Single(await _catalogue.GetArtists(CancellationToken.None)).Disambiguation);
	}

	[Fact]
	public async Task GetArtists_returns_every_external_id_of_an_artist()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new SubsonicId("art-tool-dupe"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		await SaveArtists(tool);

		var artist = Assert.Single(await _catalogue.GetArtists(CancellationToken.None));

		Assert.Equivalent(tool.ExternalIds, artist.ExternalIds);
	}

	[Fact]
	public async Task SaveArtist_allows_the_same_value_under_different_providers()
	{
		await SaveArtists(
			new Artist { ExternalIds = [new SubsonicId("shared")], Name = "Tool" },
			new Artist { ExternalIds = [new MusicBrainzArtistId("shared")], Name = "Puscifer" }
		);

		Assert.Equal(2, (await _catalogue.GetArtists(CancellationToken.None)).Count);
	}

	[Fact]
	public async Task GetAlbums_returns_added_albums_with_credits_in_position_order()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await SaveArtists(tool, puscifer);
		var split = new Album
		{
			ExternalIds = [new SubsonicId("alb-split"), new MusicBrainzReleaseId("mb-split")],
			Title = "Split",
			Year = 2010,
			IsCompilation = false,
			Artists = [Credit(puscifer, 2), Credit(tool, 1)],
		};
		await SaveAlbums(split);

		var album = Assert.Single(await _catalogue.GetAlbums(CancellationToken.None));

		Assert.Equal(split.Id, album.Id);
		Assert.Equal([new SubsonicId("alb-split"), new MusicBrainzReleaseId("mb-split")], album.ExternalIds);
		Assert.Equal("Split", album.Title);
		Assert.Equal(2010, album.Year);
		Assert.False(album.IsCompilation);
		Assert.Equal([1, 2], album.Artists.Select(a => a.Position));
		Assert.Equal(["Tool", "Puscifer"], album.Artists.Select(a => a.Artist.Name));
		Assert.Equal([tool.Id, puscifer.Id], album.Artists.Select(a => a.Artist.Id));
		Assert.Equal([new SubsonicId("art-tool"), new SubsonicId("art-puscifer")], album.Artists.SelectMany(a => a.Artist.ExternalIds));
		Assert.Equal("Tool", album.PrimaryArtist()?.Name);
	}

	[Fact]
	public async Task GetAlbums_returns_albums_ordered_by_title_each_with_only_its_own_credits()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await SaveArtists(tool, puscifer);
		await SaveAlbums(
			Album("alb-s", "Salival", 2000, Credit(tool, 1)),
			Album("alb-c", "Conditions of My Parole", 2011, Credit(puscifer, 1)),
			Album("alb-l", "Lateralus", 2001, Credit(tool, 1))
		);

		var albums = await _catalogue.GetAlbums(CancellationToken.None);

		Assert.Equal(["Conditions of My Parole", "Lateralus", "Salival"], albums.Select(a => a.Title));
		Assert.Equal(["Puscifer", "Tool", "Tool"], albums.Select(a => Assert.Single(a.Artists).Artist.Name));
	}

	[Fact]
	public async Task GetAlbums_returns_an_album_with_no_credits_as_an_empty_list()
	{
		await SaveAlbums(new Album { Title = "Wanted", Year = 1999 });

		var album = Assert.Single(await _catalogue.GetAlbums(CancellationToken.None));
		Assert.Empty(album.ExternalIds);
		Assert.Empty(album.Artists);
	}

	[Fact]
	public async Task GetArtistAlbums_returns_only_albums_credited_to_that_artist_ordered_by_title()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await SaveArtists(tool, puscifer);
		await SaveAlbums(
			Album("alb-s", "Salival", 2000, Credit(tool, 1)),
			Album("alb-c", "Conditions of My Parole", 2011, Credit(puscifer, 1)),
			Album("alb-l", "Lateralus", 2001, Credit(tool, 1))
		);

		var albums = await _catalogue.GetArtistAlbums(tool.Id, CancellationToken.None);

		Assert.Equal(["Lateralus", "Salival"], albums.Select(a => a.Title));
	}

	[Fact]
	public async Task GetArtistAlbums_returns_every_credit_on_a_shared_album()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await SaveArtists(tool, puscifer);
		await SaveAlbums(Album("alb-split", "Split", 2010, Credit(tool, 1), Credit(puscifer, 2)));

		var album = Assert.Single(await _catalogue.GetArtistAlbums(puscifer.Id, CancellationToken.None));

		Assert.Equal(["Tool", "Puscifer"], album.Artists.Select(a => a.Artist.Name));
		Assert.Equal("Tool", album.PrimaryArtist()?.Name);
	}

	[Fact]
	public async Task GetArtistAlbums_returns_external_ids_of_the_album_and_every_credit()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		var shihad = Artist("art-shihad", "Shihad");
		await SaveArtists(tool, puscifer, shihad);
		await SaveAlbums(
			Album("alb-split", "Split", 2010, Credit(tool, 1), Credit(puscifer, 2)),
			Album("alb-killjoy", "Killjoy", 1995, Credit(shihad, 1))
		);

		var album = Assert.Single(await _catalogue.GetArtistAlbums(puscifer.Id, CancellationToken.None));

		Assert.Equal([new SubsonicId("alb-split")], album.ExternalIds);
		Assert.Equal([new SubsonicId("art-tool"), new SubsonicId("art-puscifer")], album.Artists.SelectMany(a => a.Artist.ExternalIds));
	}

	[Fact]
	public async Task GetArtistAlbums_is_empty_for_an_artist_with_no_albums()
	{
		var tool = Artist("art-tool", "Tool");
		await SaveArtists(tool);
		await SaveAlbums(new Album { Title = "Wanted", Year = 1999 });

		Assert.Empty(await _catalogue.GetArtistAlbums(tool.Id, CancellationToken.None));
	}

	[Fact]
	public async Task GetArtistAlbums_is_empty_for_an_unknown_artist()
	{
		Assert.Empty(await _catalogue.GetArtistAlbums(ArtistId.New(), CancellationToken.None));
	}

	[Fact]
	public async Task GetArtist_returns_the_artist_with_only_its_own_external_ids()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		await SaveArtists(tool, Artist("art-puscifer", "Puscifer"));

		var artist = await _catalogue.GetArtist(tool.Id, CancellationToken.None);

		Assert.NotNull(artist);
		Assert.Equal(tool.Id, artist.Id);
		Assert.Equal("Tool", artist.Name);
		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], artist.ExternalIds.OrderBy(id => id.Provider));
	}

	[Fact]
	public async Task GetArtist_is_null_for_an_unknown_artist()
	{
		Assert.Null(await _catalogue.GetArtist(ArtistId.New(), CancellationToken.None));
	}

	[Fact]
	public async Task GetAlbum_returns_the_album_with_its_credits_in_position_order_and_external_ids()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		var shihad = Artist("art-shihad", "Shihad");
		await SaveArtists(tool, puscifer, shihad);
		var split = Album("alb-split", "Split", 2010, Credit(puscifer, 2), Credit(tool, 1));
		await SaveAlbums(split, Album("alb-killjoy", "Killjoy", 1995, Credit(shihad, 1)));

		var album = await _catalogue.GetAlbum(split.Id, CancellationToken.None);

		Assert.NotNull(album);
		Assert.Equal(split.Id, album.Id);
		Assert.Equal("Split", album.Title);
		Assert.Equal(2010, album.Year);
		Assert.Equal([new SubsonicId("alb-split")], album.ExternalIds);
		Assert.Equal(["Tool", "Puscifer"], album.Artists.Select(a => a.Artist.Name));
		Assert.Equal([new SubsonicId("art-tool"), new SubsonicId("art-puscifer")], album.Artists.SelectMany(a => a.Artist.ExternalIds));
	}

	[Fact]
	public async Task GetAlbum_returns_an_album_with_no_credits_as_an_empty_list()
	{
		var wanted = new Album { Title = "Wanted", Year = 1999 };
		await SaveAlbums(wanted);

		var album = await _catalogue.GetAlbum(wanted.Id, CancellationToken.None);

		Assert.NotNull(album);
		Assert.Empty(album.Artists);
	}

	[Fact]
	public async Task GetAlbum_is_null_for_an_unknown_album()
	{
		Assert.Null(await _catalogue.GetAlbum(AlbumId.New(), CancellationToken.None));
	}

	[Fact]
	public async Task GetAlbumArtists_returns_only_that_albums_artists_in_position_order_with_external_ids()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		var shihad = Artist("art-shihad", "Shihad");
		await SaveArtists(tool, puscifer, shihad);
		var split = Album("alb-split", "Split", 2010, Credit(puscifer, 2), Credit(tool, 1));
		await SaveAlbums(split, Album("alb-killjoy", "Killjoy", 1995, Credit(shihad, 1)));

		var artists = await _catalogue.GetAlbumArtists(split.Id, CancellationToken.None);

		Assert.Equal([tool.Id, puscifer.Id], artists.Select(a => a.Id));
		Assert.Equal(["Tool", "Puscifer"], artists.Select(a => a.Name));
		Assert.Equal([new SubsonicId("art-tool"), new SubsonicId("art-puscifer")], artists.SelectMany(a => a.ExternalIds));
	}

	[Fact]
	public async Task GetAlbumArtists_is_empty_for_an_unknown_album()
	{
		Assert.Empty(await _catalogue.GetAlbumArtists(AlbumId.New(), CancellationToken.None));
	}

	[Fact]
	public async Task SaveAlbum_rejects_an_album_crediting_an_artist_that_is_not_in_the_catalogue()
	{
		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.SaveAlbum(Album("alb-1", "Salival", 2000, Credit(Artist("art-tool", "Tool"), 1)), CancellationToken.None));

		Assert.Empty(await _catalogue.GetAlbums(CancellationToken.None));
	}

	[Fact]
	public async Task SaveAlbum_rejects_a_different_album_with_the_same_external_id()
	{
		await SaveAlbums(Album("alb-1", "Salival", 2000));

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.SaveAlbum(Album("alb-1", "Salival again", 2000), CancellationToken.None));

		Assert.Equal(["Salival"], (await _catalogue.GetAlbums(CancellationToken.None)).Select(a => a.Title));
	}

	[Fact]
	public async Task SaveAlbum_round_trips_the_tracked_flag()
	{
		var wanted = new Album { Title = "Wanted", Year = 1999, Tracked = true };
		var ignored = new Album { Title = "Ignored", Year = 2003 };
		await SaveAlbums(wanted, ignored);

		Assert.True((await _catalogue.GetAlbum(wanted.Id, CancellationToken.None))!.Tracked);
		Assert.False((await _catalogue.GetAlbum(ignored.Id, CancellationToken.None))!.Tracked);
		Assert.Equal([false, true], (await _catalogue.GetAlbums(CancellationToken.None)).Select(album => album.Tracked));

		wanted.Tracked = false;
		await SaveAlbums(wanted);

		Assert.False((await _catalogue.GetAlbum(wanted.Id, CancellationToken.None))!.Tracked);
	}

	[Fact]
	public async Task SaveAlbum_round_trips_compilations()
	{
		var various = Artist("art-various", "Various Artists");
		await SaveArtists(various);
		await SaveAlbums(new Album
		{
			ExternalIds = [new SubsonicId("alb-natures-best")],
			Title = "Nature's Best",
			Year = 2002,
			IsCompilation = true,
			Artists = [Credit(various, 1)],
		});

		var album = Assert.Single(await _catalogue.GetAlbums(CancellationToken.None));
		Assert.True(album.IsCompilation);
		Assert.Null(album.PrimaryArtist());
	}

	[Fact]
	public async Task SaveArtist_updates_the_name_and_replaces_the_external_ids_of_an_existing_artist()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await SaveArtists(tool, puscifer);
		tool.Name = "TOOL";
		tool.ExternalIds = [new SubsonicId("art-tool-rescanned"), new MusicBrainzArtistId("mb-tool")];

		await _catalogue.SaveArtist(tool, CancellationToken.None);

		var artists = await _catalogue.GetArtists(CancellationToken.None);
		Assert.Equal([puscifer.Id, tool.Id], artists.Select(a => a.Id));
		Assert.Equal(["Puscifer", "TOOL"], artists.Select(a => a.Name));
		Assert.Equal([new SubsonicId("art-puscifer")], artists[0].ExternalIds);
		Assert.Equal([new SubsonicId("art-tool-rescanned"), new MusicBrainzArtistId("mb-tool")], artists[1].ExternalIds);
	}

	[Fact]
	public async Task SaveArtist_changes_nothing_when_an_external_id_belongs_to_another_artist()
	{
		var tool = Artist("art-tool", "Tool");
		await SaveArtists(tool, new Artist { ExternalIds = [new MusicBrainzArtistId("mb-puscifer")], Name = "Puscifer" });
		tool.Name = "TOOL";
		tool.ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-puscifer")];

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.SaveArtist(tool, CancellationToken.None));

		var artists = await _catalogue.GetArtists(CancellationToken.None);
		Assert.Equal(["Puscifer", "Tool"], artists.Select(a => a.Name));
		Assert.Equal([new MusicBrainzArtistId("mb-puscifer"), new SubsonicId("art-tool")], artists.SelectMany(a => a.ExternalIds));
	}

	[Fact]
	public async Task SaveAlbum_updates_the_fields_and_replaces_the_external_ids_and_credits_of_an_existing_album()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await SaveArtists(tool, puscifer);
		var split = Album("alb-split", "Split", 2010, Credit(tool, 1));
		await SaveAlbums(split, Album("alb-s", "Salival", 2000, Credit(tool, 1)));
		split.Title = "Split EP";
		split.Year = 2011;
		split.IsCompilation = true;
		split.ExternalIds = [new SubsonicId("alb-split-rescanned"), new MusicBrainzReleaseId("mb-split")];
		split.Artists = [Credit(puscifer, 1), Credit(tool, 2)];

		await _catalogue.SaveAlbum(split, CancellationToken.None);

		var albums = await _catalogue.GetAlbums(CancellationToken.None);
		Assert.Equal(["Salival", "Split EP"], albums.Select(a => a.Title));
		Assert.Equal([new SubsonicId("alb-s")], albums[0].ExternalIds);
		Assert.Equal(["Tool"], albums[0].Artists.Select(a => a.Artist.Name));
		Assert.Equal(split.Id, albums[1].Id);
		Assert.Equal(2011, albums[1].Year);
		Assert.True(albums[1].IsCompilation);
		Assert.Equal([new SubsonicId("alb-split-rescanned"), new MusicBrainzReleaseId("mb-split")], albums[1].ExternalIds);
		Assert.Equal(["Puscifer", "Tool"], albums[1].Artists.Select(a => a.Artist.Name));
	}

	[Fact]
	public async Task SaveAlbum_changes_nothing_when_an_external_id_belongs_to_another_album()
	{
		var salival = Album("alb-s", "Salival", 2000);
		await SaveAlbums(salival, Album("alb-l", "Lateralus", 2001));
		salival.Title = "Salival (Live)";
		salival.ExternalIds = [new SubsonicId("alb-s"), new SubsonicId("alb-l")];

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.SaveAlbum(salival, CancellationToken.None));

		var albums = await _catalogue.GetAlbums(CancellationToken.None);
		Assert.Equal(["Lateralus", "Salival"], albums.Select(a => a.Title));
		Assert.Equal([new SubsonicId("alb-l"), new SubsonicId("alb-s")], albums.SelectMany(a => a.ExternalIds));
	}

	[Fact]
	public async Task MergeAlbums_deletes_the_absorbed_album_and_saves_the_survivor_with_its_ids()
	{
		var tool = Artist("art-tool", "Tool");
		var owned = Album("alb-l", "Lateralus (Remaster)", 2019, Credit(tool, 1));
		var untracked = new Album { ExternalIds = [new MusicBrainzReleaseGroupId("rg-l")], Title = "Lateralus", Year = 2001, Artists = [Credit(tool, 1)] };
		await SaveArtists(tool);
		await SaveAlbums(owned, untracked);

		owned.Title = "Lateralus";
		owned.ExternalIds = [new SubsonicId("alb-l"), new MusicBrainzReleaseGroupId("rg-l")];
		await _catalogue.MergeAlbums(owned, untracked.Id, [], CancellationToken.None);

		var album = Assert.Single(await _catalogue.GetAlbums(CancellationToken.None));
		Assert.Equal(owned.Id, album.Id);
		Assert.Equal("Lateralus", album.Title);
		Assert.Equal([new SubsonicId("alb-l"), new MusicBrainzReleaseGroupId("rg-l")], album.ExternalIds.OrderByDescending(id => id.Provider == Provider.Subsonic));
		Assert.Equal(tool.Id, Assert.Single(album.Artists).Artist.Id);
	}

	[Fact]
	public async Task MergeAlbums_replaces_the_tracks_of_both_albums_with_the_given_tracks()
	{
		var tool = Artist("art-tool", "Tool");
		var owned = Album("alb-l", "Lateralus (Remaster)", 2019, Credit(tool, 1));
		var vinyl = Album("alb-l-vinyl", "Lateralus", 2001, Credit(tool, 1));
		var salival = Album("alb-s", "Salival", 2000, Credit(tool, 1));
		await SaveArtists(tool);
		await SaveAlbums(owned, vinyl, salival);
		var grudge = Track(owned, 1, 1, "The Grudge", TrackCredit(tool, 1));
		var vinylGrudge = Track(vinyl, 1, 1, "The Grudge");
		var vinylBonus = Track(vinyl, 2, 1, "Bonus", TrackCredit(tool, 1));
		vinylBonus.ExternalIds = [new SubsonicId("song-bonus")];
		await _catalogue.SaveAlbumTracks(owned.Id, [grudge, Track(owned, 1, 2, "Dropped")], CancellationToken.None);
		await _catalogue.SaveAlbumTracks(vinyl.Id, [vinylGrudge, vinylBonus], CancellationToken.None);
		await _catalogue.SaveAlbumTracks(salival.Id, [Track(salival, 1, 1, "Third Eye")], CancellationToken.None);

		var movedBonus = new Track
		{
			Id = vinylBonus.Id, AlbumId = owned.Id, Title = "Bonus", DiscNumber = 2, Position = 1,
			ExternalIds = vinylBonus.ExternalIds, Artists = vinylBonus.Artists,
		};
		owned.ExternalIds = [new SubsonicId("alb-l"), new SubsonicId("alb-l-vinyl")];
		await _catalogue.MergeAlbums(owned, vinyl.Id, [grudge, movedBonus], CancellationToken.None);

		var tracks = await _catalogue.GetAlbumTracks(owned.Id, CancellationToken.None);
		Assert.Equal([grudge.Id, vinylBonus.Id], tracks.Select(track => track.Id));
		Assert.Equal([new SubsonicId("song-bonus")], tracks[1].ExternalIds);
		Assert.Equal(tool.Id, Assert.Single(tracks[1].Artists).Artist.Id);
		Assert.Empty(await _catalogue.GetAlbumTracks(vinyl.Id, CancellationToken.None));
		Assert.Equal("Third Eye", Assert.Single(await _catalogue.GetAlbumTracks(salival.Id, CancellationToken.None)).Title);
	}

	[Fact]
	public async Task MergeAlbums_rejects_a_track_of_another_album_and_changes_nothing()
	{
		var owned = Album("alb-l", "Lateralus (Remaster)", 2019);
		var vinyl = Album("alb-l-vinyl", "Lateralus", 2001);
		await SaveAlbums(owned, vinyl);
		await _catalogue.SaveAlbumTracks(vinyl.Id, [Track(vinyl, 1, 1, "The Grudge")], CancellationToken.None);

		await Assert.ThrowsAsync<ArgumentException>(
			() => _catalogue.MergeAlbums(owned, vinyl.Id, [Track(vinyl, 1, 1, "The Grudge")], CancellationToken.None));

		Assert.Equal(2, (await _catalogue.GetAlbums(CancellationToken.None)).Count);
		Assert.Single(await _catalogue.GetAlbumTracks(vinyl.Id, CancellationToken.None));
	}

	[Fact]
	public async Task MergeArtists_moves_the_absorbed_artists_track_credits_to_the_survivor()
	{
		var library = Artist("art-tool", "TOOL");
		var credited = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		var puscifer = Artist("art-puscifer", "Puscifer");
		var lateralus = Album("alb-l", "Lateralus", 2001, Credit(library, 1));
		await SaveArtists(library, credited, puscifer);
		await SaveAlbums(lateralus);
		await _catalogue.SaveAlbumTracks(
			lateralus.Id,
			[
				Track(lateralus, 1, 1, "Credited only", TrackCredit(puscifer, 1), TrackCredit(credited, 2)),
				Track(lateralus, 1, 2, "Both", TrackCredit(library, 1), TrackCredit(credited, 2)),
			],
			CancellationToken.None);

		library.ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")];
		await _catalogue.MergeArtists(library, credited.Id, CancellationToken.None);

		var tracks = await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None);
		Assert.Equal([puscifer.Id, library.Id], tracks[0].Artists.Select(credit => credit.Artist.Id));
		Assert.Equal([library.Id], tracks[1].Artists.Select(credit => credit.Artist.Id));
	}

	[Fact]
	public async Task MergeArtists_moves_the_absorbed_artists_credits_and_ids_to_the_survivor()
	{
		var library = Artist("art-tool", "TOOL");
		var credited = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		var puscifer = Artist("art-puscifer", "Puscifer");
		await SaveArtists(library, credited, puscifer);
		await SaveAlbums(
			Album("alb-l", "Lateralus", 2001, Credit(library, 1)),
			Album("alb-u", "Undertow", 1993, Credit(puscifer, 1), Credit(credited, 2)),
			Album("alb-s", "Salival", 2000, Credit(library, 1), Credit(credited, 2)));

		library.Name = "Tool";
		library.ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")];
		await _catalogue.MergeArtists(library, credited.Id, CancellationToken.None);

		var artists = await _catalogue.GetArtists(CancellationToken.None);
		Assert.Equal([puscifer.Id, library.Id], artists.Select(a => a.Id));
		Assert.Equal(2, artists[1].ExternalIds.Count);
		Assert.Contains(new MusicBrainzArtistId("mb-tool"), artists[1].ExternalIds);

		var albums = await _catalogue.GetArtistAlbums(library.Id, CancellationToken.None);
		Assert.Equal(["Lateralus", "Salival", "Undertow"], albums.Select(a => a.Title));
		Assert.Equal([library.Id], albums[1].Artists.Select(c => c.Artist.Id));
		Assert.Equal([puscifer.Id, library.Id], albums[2].Artists.Select(c => c.Artist.Id));
	}

	[Fact]
	public async Task MergeArtists_changes_nothing_when_an_external_id_belongs_to_a_third_artist()
	{
		var library = Artist("art-tool", "TOOL");
		var credited = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		var puscifer = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-puscifer")], Name = "Puscifer" };
		await SaveArtists(library, credited, puscifer);

		library.ExternalIds = [new MusicBrainzArtistId("mb-puscifer")];
		await Assert.ThrowsAsync<SqliteException>(() => _catalogue.MergeArtists(library, credited.Id, CancellationToken.None));

		Assert.Equal(["Puscifer", "TOOL", "Tool"], (await _catalogue.GetArtists(CancellationToken.None)).Select(a => a.Name));
	}

	[Fact]
	public async Task GetAlbumTracks_is_empty_for_an_album_without_tracks()
	{
		var lateralus = Album("alb-l", "Lateralus", 2001);
		await SaveAlbums(lateralus);

		Assert.Empty(await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAlbumTracks_round_trips_tracks_in_disc_and_position_order_with_their_ids_and_credits()
	{
		var tool = Artist("art-tool", "Tool");
		tool.Tracked = true;
		var puscifer = Artist("art-puscifer", "Puscifer");
		var lateralus = Album("alb-l", "Lateralus", 2001, Credit(tool, 1));
		await SaveArtists(tool, puscifer);
		await SaveAlbums(lateralus);
		var schism = Track(lateralus, 1, 5, "Schism", TrackCredit(tool, 1), TrackCredit(puscifer, 2));
		schism.ExternalIds = [new SubsonicId("song-schism"), new MusicBrainzRecordingId("rec-schism")];
		schism.DurationMs = 403_000;
		var theGrudge = Track(lateralus, 1, 1, "The Grudge", TrackCredit(tool, 1));
		var bonus = Track(lateralus, 2, 1, "Bonus");

		await _catalogue.SaveAlbumTracks(lateralus.Id, [schism, bonus, theGrudge], CancellationToken.None);

		var tracks = await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None);
		Assert.Equal([theGrudge.Id, schism.Id, bonus.Id], tracks.Select(t => t.Id));
		Assert.All(tracks, t => Assert.Equal(lateralus.Id, t.AlbumId));
		Assert.Equal("Schism", tracks[1].Title);
		Assert.Equal(1, tracks[1].DiscNumber);
		Assert.Equal(5, tracks[1].Position);
		Assert.Equal(403_000, tracks[1].DurationMs);
		Assert.Equal(2, tracks[1].ExternalIds.Count);
		Assert.Contains(new SubsonicId("song-schism"), tracks[1].ExternalIds);
		Assert.Contains(new MusicBrainzRecordingId("rec-schism"), tracks[1].ExternalIds);
		Assert.Equal([1, 2], tracks[1].Artists.Select(c => c.Position));
		Assert.Equal([tool.Id, puscifer.Id], tracks[1].Artists.Select(c => c.Artist.Id));
		Assert.Equal([new SubsonicId("art-tool")], tracks[1].Artists[0].Artist.ExternalIds);
		Assert.True(tracks[1].Artists[0].Artist.Tracked);
		Assert.Null(tracks[0].DurationMs);
		Assert.Empty(tracks[2].Artists);
		Assert.Empty(tracks[2].ExternalIds);
	}

	[Fact]
	public async Task SaveAlbumTracks_replaces_the_tracks_the_album_had()
	{
		var lateralus = Album("alb-l", "Lateralus", 2001);
		await SaveAlbums(lateralus);
		var theGrudge = Track(lateralus, 1, 1, "The Grudge");
		theGrudge.ExternalIds = [new SubsonicId("song-grudge")];
		await _catalogue.SaveAlbumTracks(lateralus.Id, [theGrudge, Track(lateralus, 1, 2, "Eon Blue Apocalypse")], CancellationToken.None);

		theGrudge.Title = "The Grudge (Remaster)";
		theGrudge.ExternalIds = [new MusicBrainzRecordingId("rec-grudge")];
		await _catalogue.SaveAlbumTracks(lateralus.Id, [theGrudge], CancellationToken.None);

		var track = Assert.Single(await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None));
		Assert.Equal(theGrudge.Id, track.Id);
		Assert.Equal("The Grudge (Remaster)", track.Title);
		Assert.Equal([new MusicBrainzRecordingId("rec-grudge")], track.ExternalIds);
	}

	[Fact]
	public async Task SaveAlbumTracks_leaves_the_tracks_of_other_albums_alone()
	{
		var lateralus = Album("alb-l", "Lateralus", 2001);
		var salival = Album("alb-s", "Salival", 2000);
		await SaveAlbums(lateralus, salival);
		await _catalogue.SaveAlbumTracks(salival.Id, [Track(salival, 1, 1, "Third Eye")], CancellationToken.None);

		await _catalogue.SaveAlbumTracks(lateralus.Id, [Track(lateralus, 1, 1, "The Grudge")], CancellationToken.None);
		await _catalogue.SaveAlbumTracks(lateralus.Id, [], CancellationToken.None);

		Assert.Empty(await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None));
		Assert.Equal("Third Eye", Assert.Single(await _catalogue.GetAlbumTracks(salival.Id, CancellationToken.None)).Title);
	}

	[Fact]
	public async Task SaveAlbumTracks_allows_the_same_recording_on_two_albums_and_a_repeated_position_within_one()
	{
		var lateralus = Album("alb-l", "Lateralus", 2001);
		var salival = Album("alb-s", "Salival", 2000);
		await SaveAlbums(lateralus, salival);
		var studio = Track(lateralus, 1, 1, "Third Eye");
		var alternate = Track(lateralus, 1, 1, "Third Eye (Alternate)");
		var live = Track(salival, 1, 1, "Third Eye");
		studio.ExternalIds = [new MusicBrainzRecordingId("rec-third-eye")];
		live.ExternalIds = [new MusicBrainzRecordingId("rec-third-eye")];

		await _catalogue.SaveAlbumTracks(lateralus.Id, [studio, alternate], CancellationToken.None);
		await _catalogue.SaveAlbumTracks(salival.Id, [live], CancellationToken.None);

		Assert.Equal(["Third Eye", "Third Eye (Alternate)"], (await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None)).Select(t => t.Title));
		Assert.Equal([new MusicBrainzRecordingId("rec-third-eye")], Assert.Single(await _catalogue.GetAlbumTracks(salival.Id, CancellationToken.None)).ExternalIds);
	}

	[Fact]
	public async Task SaveAlbumTracks_rejects_a_track_of_another_album_and_changes_nothing()
	{
		var lateralus = Album("alb-l", "Lateralus", 2001);
		var salival = Album("alb-s", "Salival", 2000);
		await SaveAlbums(lateralus, salival);
		await _catalogue.SaveAlbumTracks(lateralus.Id, [Track(lateralus, 1, 1, "The Grudge")], CancellationToken.None);

		await Assert.ThrowsAsync<ArgumentException>(
			() => _catalogue.SaveAlbumTracks(lateralus.Id, [Track(salival, 1, 1, "Third Eye")], CancellationToken.None));

		Assert.Equal("The Grudge", Assert.Single(await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None)).Title);
	}

	[Fact]
	public async Task SaveAlbumTracks_changes_nothing_when_a_track_credits_an_artist_that_is_not_in_the_catalogue()
	{
		var lateralus = Album("alb-l", "Lateralus", 2001);
		await SaveAlbums(lateralus);
		await _catalogue.SaveAlbumTracks(lateralus.Id, [Track(lateralus, 1, 1, "The Grudge")], CancellationToken.None);

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.SaveAlbumTracks(lateralus.Id, [Track(lateralus, 1, 1, "Schism", TrackCredit(Artist("art-tool", "Tool"), 1))], CancellationToken.None));

		Assert.Equal("The Grudge", Assert.Single(await _catalogue.GetAlbumTracks(lateralus.Id, CancellationToken.None)).Title);
	}

	private async Task SaveArtists(params Artist[] artists)
	{
		foreach (var artist in artists)
		{
			await _catalogue.SaveArtist(artist, CancellationToken.None);
		}
	}

	private async Task SaveAlbums(params Album[] albums)
	{
		foreach (var album in albums)
		{
			await _catalogue.SaveAlbum(album, CancellationToken.None);
		}
	}

	[Fact]
	public async Task FindTrackAlbums_returns_the_album_of_each_track_holding_one_of_the_ids()
	{
		var tool = Artist("art-tool", "Tool");
		var lateralus = Album("alb-lateralus", "Lateralus", 2001, Credit(tool, 1));
		var aenima = Album("alb-aenima", "Ænima", 1996, Credit(tool, 1));
		await _catalogue.SaveArtist(tool, CancellationToken.None);
		await _catalogue.SaveAlbum(lateralus, CancellationToken.None);
		await _catalogue.SaveAlbum(aenima, CancellationToken.None);
		var schism = Track(lateralus, 1, 5, "Schism");
		schism.ExternalIds = [new SpotifyTrackId("sp-schism"), new Isrc("USVR10100102")];
		var stinkfist = Track(aenima, 1, 1, "Stinkfist");
		stinkfist.ExternalIds = [new SpotifyTrackId("sp-stinkfist")];
		await _catalogue.SaveAlbumTracks(lateralus.Id, [schism, Track(lateralus, 1, 1, "The Grudge")], CancellationToken.None);
		await _catalogue.SaveAlbumTracks(aenima.Id, [stinkfist], CancellationToken.None);

		var found = await _catalogue.FindTrackAlbums(
			[new SpotifyTrackId("sp-schism"), new SpotifyTrackId("sp-stinkfist"), new SpotifyTrackId("sp-unknown"), new Isrc("USVR10100102"), new SpotifyTrackId("sp-schism")],
			CancellationToken.None);

		Assert.Equal(3, found.Count);
		Assert.Equal(lateralus.Id, found[new SpotifyTrackId("sp-schism")]);
		Assert.Equal(lateralus.Id, found[new Isrc("USVR10100102")]);
		Assert.Equal(aenima.Id, found[new SpotifyTrackId("sp-stinkfist")]);
	}

	[Fact]
	public async Task FindTrackAlbums_does_not_confuse_the_same_value_under_another_provider()
	{
		var lateralus = Album("alb-lateralus", "Lateralus", 2001);
		await _catalogue.SaveAlbum(lateralus, CancellationToken.None);
		var schism = Track(lateralus, 1, 5, "Schism");
		schism.ExternalIds = [new SubsonicId("shared-value")];
		await _catalogue.SaveAlbumTracks(lateralus.Id, [schism], CancellationToken.None);

		Assert.Empty(await _catalogue.FindTrackAlbums([new SpotifyTrackId("shared-value")], CancellationToken.None));
		Assert.Empty(await _catalogue.FindTrackAlbums([], CancellationToken.None));
	}

	[Fact]
	public async Task FindArtists_returns_the_artist_holding_each_of_the_ids()
	{
		var tool = Artist("art-tool", "Tool");
		tool.ExternalIds = [new SubsonicId("art-tool"), new SpotifyArtistId("sp-tool")];
		var puscifer = Artist("art-puscifer", "Puscifer");
		await _catalogue.SaveArtist(tool, CancellationToken.None);
		await _catalogue.SaveArtist(puscifer, CancellationToken.None);

		var found = await _catalogue.FindArtists([new SpotifyArtistId("sp-tool"), new SubsonicId("art-puscifer"), new SpotifyArtistId("sp-unknown")], CancellationToken.None);

		Assert.Equal(2, found.Count);
		Assert.Equal(tool.Id, found[new SpotifyArtistId("sp-tool")]);
		Assert.Equal(puscifer.Id, found[new SubsonicId("art-puscifer")]);
	}

	private static Artist Artist(string subsonicId, string name)
	{
		return new Artist { ExternalIds = [new SubsonicId(subsonicId)], Name = name };
	}

	private static AlbumArtist Credit(Artist artist, int position)
	{
		return new AlbumArtist { Artist = artist, Position = position };
	}

	private static TrackArtist TrackCredit(Artist artist, int position)
	{
		return new TrackArtist { Artist = artist, Position = position };
	}

	private static Track Track(Album album, int discNumber, int position, string title, params TrackArtist[] credits)
	{
		return new Track { AlbumId = album.Id, DiscNumber = discNumber, Position = position, Title = title, Artists = credits };
	}

	private static Album Album(string subsonicId, string title, int year, params AlbumArtist[] credits)
	{
		return new Album { ExternalIds = [new SubsonicId(subsonicId)], Title = title, Year = year, Artists = credits };
	}
}
