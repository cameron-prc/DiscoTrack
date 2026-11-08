using System.ComponentModel.DataAnnotations;

namespace DiscoTrack.Infrastructure.Sqlite;

public class SqliteOptions
{
	public const string ConnectionStringName = "Default";

	[Required]
	public required string ConnectionString { get; set; }
}
