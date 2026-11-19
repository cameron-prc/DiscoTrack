using DiscoTrack.Catalogue;
using DiscoTrack.Discography;
using DiscoTrack.Infrastructure.Catalogue;
using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Discography;
using DiscoTrack.Infrastructure.Jobs;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Tests.Clients.MusicBrainz;
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
	public void AddMusicBrainzDiscographySource_resolves_a_discography_source()
	{
		using var provider = BuildMusicBrainzProvider(TestMusicBrainzOptions.Contact);
		using var scope = provider.CreateScope();

		var source = scope.ServiceProvider.GetRequiredService<IDiscographySource>();

		Assert.IsType<MusicBrainzDiscographySource>(source);
	}

	[Fact]
	public void AddMusicBrainzDiscographySource_identifies_the_application_and_contact_to_musicbrainz()
	{
		using var provider = BuildMusicBrainzProvider(TestMusicBrainzOptions.Contact);

		var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IMusicBrainzClient));

		Assert.Equal(new Uri(TestMusicBrainzOptions.BaseUrl), http.BaseAddress);
		Assert.Matches($@"^DiscoTrack/\d+\.\d+\.\d+ \({TestMusicBrainzOptions.Contact}\)$", http.DefaultRequestHeaders.UserAgent.ToString());
	}

	[Fact]
	public void AddMusicBrainzDiscographySource_shares_one_rate_limiter_across_scopes()
	{
		using var provider = BuildMusicBrainzProvider(TestMusicBrainzOptions.Contact);
		using var first = provider.CreateScope();
		using var second = provider.CreateScope();

		var rateLimiter = first.ServiceProvider.GetRequiredService<IMusicBrainzRateLimiter>();

		Assert.Same(rateLimiter, second.ServiceProvider.GetRequiredService<IMusicBrainzRateLimiter>());
	}

	[Fact]
	public void AddMusicBrainzDiscographySource_rejects_a_missing_contact()
	{
		using var provider = BuildMusicBrainzProvider(contact: null);

		var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MusicBrainzOptions>>().Value);
		Assert.Contains("Contact", ex.Message);
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

	private static ServiceProvider BuildMusicBrainzProvider(string? contact)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[$"{MusicBrainzOptions.Section}:BaseUrl"] = TestMusicBrainzOptions.BaseUrl,
				[$"{MusicBrainzOptions.Section}:Contact"] = contact,
			})
			.Build();

		return new ServiceCollection()
			.AddMusicBrainzDiscographySource(configuration)
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
