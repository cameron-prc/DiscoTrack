using DiscoTrack.Infrastructure.Sqlite.Migrations;

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
		var applied = Migrator.Migrate(_db.ConnectionString);

		Assert.Equal(new[] { "DiscoTrack.Infrastructure.Sqlite.Migrations.0001_create_library_tables.sql" }, applied);
		Assert.True(Migrator.IsUpToDate(_db.ConnectionString));
	}

	[Fact]
	public void Migrate_creates_the_library_tables()
	{
		Migrator.Migrate(_db.ConnectionString);

		Assert.Superset(new HashSet<string> { "Artists", "Albums", "AlbumArtists" }, _db.TableNames().ToHashSet());
	}

	[Fact]
	public void Migrate_on_a_current_database_applies_nothing()
	{
		Migrator.Migrate(_db.ConnectionString);

		Assert.Empty(Migrator.Migrate(_db.ConnectionString));
	}
}
