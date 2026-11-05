using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;
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
	public async Task AddArtists_with_nothing_does_nothing()
	{
		await _catalogue.AddArtists([], CancellationToken.None);

		Assert.Empty(await _catalogue.GetArtists(CancellationToken.None));
	}

	[Fact]
	public async Task GetArtists_returns_added_artists_ordered_by_name()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await _catalogue.AddArtists([tool, puscifer], CancellationToken.None);

		var artists = await _catalogue.GetArtists(CancellationToken.None);

		Assert.Equal(["Puscifer", "Tool"], artists.Select(a => a.Name));
		Assert.Equal(["art-puscifer", "art-tool"], artists.Select(a => a.SubsonicId));
		Assert.Equal([puscifer.Id, tool.Id], artists.Select(a => a.Id));
	}

	[Fact]
	public async Task AddArtists_saves_a_whole_batch()
	{
		var batch = Enumerable.Range(1, 25)
			.Select(i => Artist($"art-{i:D2}", $"Artist {i:D2}"))
			.ToList();

		await _catalogue.AddArtists(batch, CancellationToken.None);

		var artists = await _catalogue.GetArtists(CancellationToken.None);
		Assert.Equal(batch.Select(a => a.Id), artists.Select(a => a.Id));
	}

	[Fact]
	public async Task AddArtists_saves_an_artist_without_a_subsonic_id()
	{
		await _catalogue.AddArtists([new Artist { Name = "Shihad" }], CancellationToken.None);

		var artist = Assert.Single(await _catalogue.GetArtists(CancellationToken.None));
		Assert.Null(artist.SubsonicId);
		Assert.Equal("Shihad", artist.Name);
	}

	[Fact]
	public async Task AddArtists_rejects_an_artist_that_is_already_in_the_catalogue()
	{
		var tool = Artist("art-tool", "Tool");
		await _catalogue.AddArtists([tool], CancellationToken.None);

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.AddArtists([tool], CancellationToken.None));
	}

	[Fact]
	public async Task AddArtists_rejects_a_different_artist_with_the_same_subsonic_id()
	{
		await _catalogue.AddArtists([Artist("art-tool", "Tool")], CancellationToken.None);

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.AddArtists([Artist("art-tool", "Tool again")], CancellationToken.None));
	}

	[Fact]
	public async Task AddArtists_rolls_back_the_whole_batch_when_one_row_fails()
	{
		await _catalogue.AddArtists([Artist("art-tool", "Tool")], CancellationToken.None);

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.AddArtists([Artist("art-puscifer", "Puscifer"), Artist("art-tool", "Tool again")], CancellationToken.None));

		Assert.Equal(["Tool"], (await _catalogue.GetArtists(CancellationToken.None)).Select(a => a.Name));
	}

	[Fact]
	public async Task AddAlbums_with_nothing_does_nothing()
	{
		await _catalogue.AddAlbums([], CancellationToken.None);

		Assert.Empty(await _catalogue.GetAlbums(CancellationToken.None));
	}

	[Fact]
	public async Task GetAlbums_returns_added_albums_with_credits_in_position_order()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await _catalogue.AddArtists([tool, puscifer], CancellationToken.None);
		var split = new Album
		{
			SubsonicId = "alb-split",
			Title = "Split",
			Year = 2010,
			IsCompilation = false,
			Artists = [Credit(puscifer, 2), Credit(tool, 1)],
		};
		await _catalogue.AddAlbums([split], CancellationToken.None);

		var album = Assert.Single(await _catalogue.GetAlbums(CancellationToken.None));

		Assert.Equal(split.Id, album.Id);
		Assert.Equal("alb-split", album.SubsonicId);
		Assert.Equal("Split", album.Title);
		Assert.Equal(2010, album.Year);
		Assert.False(album.IsCompilation);
		Assert.Equal([1, 2], album.Artists.Select(a => a.Position));
		Assert.Equal(["Tool", "Puscifer"], album.Artists.Select(a => a.Artist.Name));
		Assert.Equal([tool.Id, puscifer.Id], album.Artists.Select(a => a.Artist.Id));
		Assert.Equal(["art-tool", "art-puscifer"], album.Artists.Select(a => a.Artist.SubsonicId));
		Assert.Equal("Tool", album.PrimaryArtist()?.Name);
	}

	[Fact]
	public async Task GetAlbums_returns_albums_ordered_by_title_each_with_only_its_own_credits()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await _catalogue.AddArtists([tool, puscifer], CancellationToken.None);
		await _catalogue.AddAlbums([
			Album("alb-s", "Salival", 2000, Credit(tool, 1)),
			Album("alb-c", "Conditions of My Parole", 2011, Credit(puscifer, 1)),
			Album("alb-l", "Lateralus", 2001, Credit(tool, 1)),
		], CancellationToken.None);

		var albums = await _catalogue.GetAlbums(CancellationToken.None);

		Assert.Equal(["Conditions of My Parole", "Lateralus", "Salival"], albums.Select(a => a.Title));
		Assert.Equal(["Puscifer", "Tool", "Tool"], albums.Select(a => Assert.Single(a.Artists).Artist.Name));
	}

	[Fact]
	public async Task GetAlbums_returns_an_album_with_no_credits_as_an_empty_list()
	{
		await _catalogue.AddAlbums([new Album { Title = "Wanted", Year = 1999 }], CancellationToken.None);

		var album = Assert.Single(await _catalogue.GetAlbums(CancellationToken.None));
		Assert.Null(album.SubsonicId);
		Assert.Empty(album.Artists);
	}

	[Fact]
	public async Task GetArtistAlbums_returns_only_albums_credited_to_that_artist_ordered_by_title()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await _catalogue.AddArtists([tool, puscifer], CancellationToken.None);
		await _catalogue.AddAlbums([
			Album("alb-s", "Salival", 2000, Credit(tool, 1)),
			Album("alb-c", "Conditions of My Parole", 2011, Credit(puscifer, 1)),
			Album("alb-l", "Lateralus", 2001, Credit(tool, 1)),
		], CancellationToken.None);

		var albums = await _catalogue.GetArtistAlbums(tool.Id, CancellationToken.None);

		Assert.Equal(["Lateralus", "Salival"], albums.Select(a => a.Title));
	}

	[Fact]
	public async Task GetArtistAlbums_returns_every_credit_on_a_shared_album()
	{
		var tool = Artist("art-tool", "Tool");
		var puscifer = Artist("art-puscifer", "Puscifer");
		await _catalogue.AddArtists([tool, puscifer], CancellationToken.None);
		await _catalogue.AddAlbums([Album("alb-split", "Split", 2010, Credit(tool, 1), Credit(puscifer, 2))], CancellationToken.None);

		var album = Assert.Single(await _catalogue.GetArtistAlbums(puscifer.Id, CancellationToken.None));

		Assert.Equal(["Tool", "Puscifer"], album.Artists.Select(a => a.Artist.Name));
		Assert.Equal("Tool", album.PrimaryArtist()?.Name);
	}

	[Fact]
	public async Task GetArtistAlbums_is_empty_for_an_artist_with_no_albums()
	{
		var tool = Artist("art-tool", "Tool");
		await _catalogue.AddArtists([tool], CancellationToken.None);
		await _catalogue.AddAlbums([new Album { Title = "Wanted", Year = 1999 }], CancellationToken.None);

		Assert.Empty(await _catalogue.GetArtistAlbums(tool.Id, CancellationToken.None));
	}

	[Fact]
	public async Task GetArtistAlbums_is_empty_for_an_unknown_artist()
	{
		Assert.Empty(await _catalogue.GetArtistAlbums(ArtistId.New(), CancellationToken.None));
	}

	[Fact]
	public async Task AddAlbums_rejects_an_album_crediting_an_artist_that_is_not_in_the_catalogue()
	{
		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.AddAlbums([Album("alb-1", "Salival", 2000, Credit(Artist("art-tool", "Tool"), 1))], CancellationToken.None));

		Assert.Empty(await _catalogue.GetAlbums(CancellationToken.None));
	}

	[Fact]
	public async Task AddAlbums_rejects_an_album_that_is_already_in_the_catalogue()
	{
		var salival = Album("alb-1", "Salival", 2000);
		await _catalogue.AddAlbums([salival], CancellationToken.None);

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.AddAlbums([salival], CancellationToken.None));
	}

	[Fact]
	public async Task AddAlbums_rolls_back_the_whole_batch_when_one_row_fails()
	{
		await _catalogue.AddAlbums([Album("alb-1", "Salival", 2000)], CancellationToken.None);

		await Assert.ThrowsAsync<SqliteException>(
			() => _catalogue.AddAlbums([
				Album("alb-2", "Lateralus", 2001),
				Album("alb-1", "Duplicate", 2002),
			], CancellationToken.None));

		Assert.Equal(["Salival"], (await _catalogue.GetAlbums(CancellationToken.None)).Select(a => a.Title));
	}

	[Fact]
	public async Task AddAlbums_round_trips_compilations()
	{
		var various = Artist("art-various", "Various Artists");
		await _catalogue.AddArtists([various], CancellationToken.None);
		await _catalogue.AddAlbums([new Album
		{
			SubsonicId = "alb-natures-best",
			Title = "Nature's Best",
			Year = 2002,
			IsCompilation = true,
			Artists = [Credit(various, 1)],
		}], CancellationToken.None);

		var album = Assert.Single(await _catalogue.GetAlbums(CancellationToken.None));
		Assert.True(album.IsCompilation);
		Assert.Null(album.PrimaryArtist());
	}

	private static Artist Artist(string subsonicId, string name)
	{
		return new Artist { SubsonicId = subsonicId, Name = name };
	}

	private static AlbumArtist Credit(Artist artist, int position)
	{
		return new AlbumArtist { Artist = artist, Position = position };
	}

	private static Album Album(string subsonicId, string title, int year, params AlbumArtist[] credits)
	{
		return new Album { SubsonicId = subsonicId, Title = title, Year = year, Artists = credits };
	}
}
