using DbUp;
using DbUp.Engine;

namespace DiscoTrack.Infrastructure.Migrations;

public static class Migrator
{
	public static void Migrate(string connectionString)
	{
		var result = Build(connectionString).PerformUpgrade();

		if (!result.Successful)
			throw new InvalidOperationException($"Migration '{result.ErrorScript?.Name}' failed.", result.Error);
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
