using System.ComponentModel.DataAnnotations;

namespace DiscoTrack.Infrastructure.Sqlite;

public class SqliteOptions
{
	public const string Section = "Sqlite";

	[Required]
	public required string ConnectionString { get; set; }
}
