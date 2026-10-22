using Microsoft.Data.Sqlite;

namespace DiscoTrack.Infrastructure.Tests;

public sealed class TestDatabase : IDisposable
{
	private readonly string _path = Path.Combine(Path.GetTempPath(), $"discotrack-{Guid.NewGuid():N}.db");

	public string ConnectionString => $"Data Source={_path}";

	public IReadOnlyList<string> TableNames()
	{
		using var connection = new SqliteConnection(ConnectionString);
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name";
		using var reader = command.ExecuteReader();

		var names = new List<string>();
		while (reader.Read())
			names.Add(reader.GetString(0));
		return names;
	}

	public void Dispose()
	{
		SqliteConnection.ClearAllPools();

		foreach (var suffix in new[] { "", "-wal", "-shm" })
			File.Delete(_path + suffix);
	}
}
