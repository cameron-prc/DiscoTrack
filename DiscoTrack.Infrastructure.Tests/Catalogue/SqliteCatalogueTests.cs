using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Catalogue;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Sqlite.Migrations;
using DiscoTrack.Infrastructure.Tests.Sqlite;
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

	[Fact]
	public async Task SaveArtist_rejects_a_different_artist_with_the_same_external_id()
	{
		await SaveArtists(Artist("art-tool", "Tool"));

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.SaveArtist(Artist("art-tool", "Tool again"), CancellationToken.None));

		Assert.Equal(["Tool"], (await _catalogue.GetArtists(CancellationToken.None)).Select(a => a.Name));
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
		await SaveArtists(tool, Artist("art-puscifer", "Puscifer"));
		tool.Name = "TOOL";
		tool.ExternalIds = [new SubsonicId("art-tool"), new SubsonicId("art-puscifer")];

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.SaveArtist(tool, CancellationToken.None));

		var artists = await _catalogue.GetArtists(CancellationToken.None);
		Assert.Equal(["Puscifer", "Tool"], artists.Select(a => a.Name));
		Assert.Equal([new SubsonicId("art-puscifer"), new SubsonicId("art-tool")], artists.SelectMany(a => a.ExternalIds));
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

	private static Artist Artist(string subsonicId, string name)
	{
		return new Artist { ExternalIds = [new SubsonicId(subsonicId)], Name = name };
	}

	private static AlbumArtist Credit(Artist artist, int position)
	{
		return new AlbumArtist { Artist = artist, Position = position };
	}

	private static Album Album(string subsonicId, string title, int year, params AlbumArtist[] credits)
	{
		return new Album { ExternalIds = [new SubsonicId(subsonicId)], Title = title, Year = year, Artists = credits };
	}
}
