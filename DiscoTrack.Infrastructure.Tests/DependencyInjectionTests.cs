using DiscoTrack.Catalogue;
using DiscoTrack.Infrastructure.Catalogue;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Jobs;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Tests.Clients.Subsonic;
using DiscoTrack.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tests;

public class DependencyInjectionTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void AddSubsonicLibrarySource_rejects_a_non_positive_page_size(int pageSize)
	{
		var options = BuildSubsonicOptions(pageSize);

		var ex = Assert.Throws<OptionsValidationException>(() => options.Value);
		Assert.Contains("PageSize", ex.Message);
	}

	[Fact]
	public void AddSubsonicLibrarySource_accepts_a_positive_page_size()
	{
		var options = BuildSubsonicOptions(25);

		Assert.Equal(25, options.Value.PageSize);
	}

	[Fact]
	public void AddSqliteCatalogue_resolves_a_catalogue()
	{
		var catalogue = BuildCatalogueProvider("Data Source=:memory:").GetRequiredService<ICatalogue>();

		Assert.IsType<SqliteCatalogue>(catalogue);
	}

	[Fact]
	public void AddInMemoryJobQueue_shares_one_queue_across_scopes()
	{
		using var provider = new ServiceCollection()
			.AddInMemoryJobQueue()
			.BuildServiceProvider();
		using var first = provider.CreateScope();
		using var second = provider.CreateScope();

		var queue = first.ServiceProvider.GetRequiredService<IJobQueue>();

		Assert.IsType<InMemoryJobQueue>(queue);
		Assert.Same(queue, second.ServiceProvider.GetRequiredService<IJobQueue>());
	}

	[Fact]
	public void AddSqlite_rejects_a_missing_connection_string()
	{
		var provider = BuildCatalogueProvider(connectionString: null);

		var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqliteOptions>>().Value);
		Assert.Contains("ConnectionString", ex.Message);
	}

	private static ServiceProvider BuildCatalogueProvider(string? connectionString)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[$"ConnectionStrings:{SqliteOptions.ConnectionStringName}"] = connectionString,
			})
			.Build();

		return new ServiceCollection()
			.AddSqlite(configuration)
			.AddSqliteCatalogue()
			.BuildServiceProvider();
	}

	private static IOptions<SubsonicOptions> BuildSubsonicOptions(int pageSize)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[$"{SubsonicOptions.Section}:BaseUrl"] = TestSubsonicOptions.BaseUrl,
				[$"{SubsonicOptions.Section}:Username"] = TestSubsonicOptions.Username,
				[$"{SubsonicOptions.Section}:Salt"] = TestSubsonicOptions.Salt,
				[$"{SubsonicOptions.Section}:Token"] = TestSubsonicOptions.Token,
				[$"{SubsonicOptions.Section}:PageSize"] = pageSize.ToString(),
			})
			.Build();

		return new ServiceCollection()
			.AddSubsonicLibrarySource(configuration)
			.BuildServiceProvider()
			.GetRequiredService<IOptions<SubsonicOptions>>();
	}
}
