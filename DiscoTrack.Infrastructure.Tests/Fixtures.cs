namespace DiscoTrack.Infrastructure.Tests;

public static class Fixtures
{
	public static string Read(string relativePath)
	{
		return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath));
	}
}
