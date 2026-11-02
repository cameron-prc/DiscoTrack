using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Tests.Clients.Subsonic;
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
