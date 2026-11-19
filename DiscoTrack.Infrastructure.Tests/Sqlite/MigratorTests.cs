using Dapper;
using DiscoTrack.Infrastructure.Sqlite.Migrations;
using Microsoft.Data.Sqlite;

namespace DiscoTrack.Infrastructure.Tests.Sqlite;

public sealed class MigratorTests : IDisposable
{
	private readonly TestDatabase _db = new();

	public void Dispose() => _db.Dispose();

	[Fact]
	public void Fresh_database_is_not_up_to_date()
	{
		Assert.False(Migrator.IsUpToDate(_db.ConnectionString));
	}

	[Fact]
	public void Migrate_applies_every_script_and_reports_them()
	{
		var scripts = typeof(Migrator).Assembly
			.GetManifestResourceNames()
			.Where(name => name.EndsWith(".sql"))
			.Order(StringComparer.Ordinal)
			.ToList();

		var applied = Migrator.Migrate(_db.ConnectionString);

		Assert.NotEmpty(scripts);
		Assert.Equal(scripts, applied);
		Assert.True(Migrator.IsUpToDate(_db.ConnectionString));
	}

	[Fact]
	public void Migrate_creates_the_library_tables()
	{
		Migrator.Migrate(_db.ConnectionString);

		Assert.Superset(new HashSet<string> { "Artists", "Albums", "AlbumArtists", "ArtistExternalIds", "AlbumExternalIds" }, _db.TableNames().ToHashSet());
	}

	[Fact]
	public void Moving_to_external_ids_keeps_existing_subsonic_ids()
	{
		using var connection = new SqliteConnection(_db.ConnectionString);
		connection.Open();
		connection.Execute(Script("0001_create_library_tables.sql"));
		connection.Execute("""
			INSERT INTO Artists (Id, SubsonicId, Name) VALUES ('artist-1', 'art-tool', 'Tool'), ('artist-2', NULL, 'Shihad');
			INSERT INTO Albums (Id, SubsonicId, Title, Year) VALUES ('album-1', 'alb-salival', 'Salival', 2000);
			""");

		connection.Execute(Script("0002_move_subsonic_ids_to_external_ids.sql"));

		Assert.Equal(
			[("artist-1", "Subsonic", "art-tool")],
			connection.Query<(string, string, string)>("SELECT ArtistId, Provider, Value FROM ArtistExternalIds"));
		Assert.Equal(
			[("album-1", "Subsonic", "alb-salival")],
			connection.Query<(string, string, string)>("SELECT AlbumId, Provider, Value FROM AlbumExternalIds"));
		Assert.Equal(2, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Artists"));
	}

	[Fact]
	public void Splitting_musicbrainz_providers_makes_artist_ids_artists_and_album_ids_releases()
	{
		using var connection = new SqliteConnection(_db.ConnectionString);
		connection.Open();
		connection.Execute(Script("0001_create_library_tables.sql"));
		connection.Execute(Script("0002_move_subsonic_ids_to_external_ids.sql"));
		connection.Execute(Script("0003_add_is_tracked.sql"));
		connection.Execute("""
			INSERT INTO Artists (Id, Name) VALUES ('artist-1', 'Tool');
			INSERT INTO Albums (Id, Title, Year) VALUES ('album-1', 'Lateralus', 2001);
			INSERT INTO ArtistExternalIds (ArtistId, Provider, Value) VALUES ('artist-1', 'Subsonic', 'art-tool'), ('artist-1', 'MusicBrainz', 'mb-tool');
			INSERT INTO AlbumExternalIds (AlbumId, Provider, Value) VALUES ('album-1', 'Subsonic', 'alb-lateralus'), ('album-1', 'MusicBrainz', 'mb-lateralus');
			""");

		connection.Execute(Script("0004_split_musicbrainz_providers.sql"));

		Assert.Equal(
			[("MusicBrainzArtist", "mb-tool"), ("Subsonic", "art-tool")],
			connection.Query<(string, string)>("SELECT Provider, Value FROM ArtistExternalIds ORDER BY Provider"));
		Assert.Equal(
			[("MusicBrainzRelease", "mb-lateralus"), ("Subsonic", "alb-lateralus")],
			connection.Query<(string, string)>("SELECT Provider, Value FROM AlbumExternalIds ORDER BY Provider"));
	}

	[Fact]
	public void Migrate_on_a_current_database_applies_nothing()
	{
		Migrator.Migrate(_db.ConnectionString);

		Assert.Empty(Migrator.Migrate(_db.ConnectionString));
	}

	private static string Script(string name)
	{
		using var stream = typeof(Migrator).Assembly.GetManifestResourceStream($"DiscoTrack.Infrastructure.Sqlite.Migrations.{name}")!;
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}
}
