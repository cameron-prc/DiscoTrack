using DbUp;
using DbUp.Engine;

namespace DiscoTrack.Infrastructure.Sqlite.Migrations;

public static class Migrator
{
	public static IReadOnlyList<string> Migrate(string connectionString)
	{
		var result = Build(connectionString).PerformUpgrade();

		if (!result.Successful)
			throw new InvalidOperationException($"Migration '{result.ErrorScript?.Name}' failed.", result.Error);

		return result.Scripts.Select(s => s.Name).ToList();
	}

	public static bool IsUpToDate(string connectionString)
		=> Build(connectionString).GetScriptsToExecute().Count == 0;

	private static UpgradeEngine Build(string connectionString)
		=> DeployChanges.To
			.SqliteDatabase(connectionString)
			.WithScriptsEmbeddedInAssembly(typeof(Migrator).Assembly)
			.WithTransactionPerScript()
			.LogToNowhere()
			.Build();
}
