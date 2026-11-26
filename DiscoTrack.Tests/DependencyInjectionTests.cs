using DiscoTrack.Catalogue;
using DiscoTrack.Discography;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using DiscoTrack.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscoTrack.Tests;

public class DependencyInjectionTests
{
	[Fact]
	public void AddJobs_resolves_a_job_runner_with_the_sync_library_handler()
	{
		using var scope = BuildProvider().CreateScope();

		var runner = scope.ServiceProvider.GetRequiredService<IJobRunner>();
		var handler = scope.ServiceProvider.GetRequiredService<ISyncLibraryHandler>();

		Assert.IsType<JobRunner>(runner);
		Assert.IsType<SyncLibraryJobHandler>(handler);
	}

	[Fact]
	public void AddJobs_resolves_the_sync_discography_album_handler()
	{
		using var scope = BuildProvider().CreateScope();

		var handler = scope.ServiceProvider.GetRequiredService<ISyncAlbumWithDiscographyJobHandler>();

		Assert.IsType<SyncAlbumWithDiscographyJobHandler>(handler);
	}

	[Fact]
	public void AddJobs_resolves_the_discography_album_resolver()
	{
		using var scope = BuildProvider().CreateScope();

		var resolver = scope.ServiceProvider.GetRequiredService<IDiscographyAlbumResolver>();

		Assert.IsType<DiscographyAlbumResolver>(resolver);
	}

	[Fact]
	public void AddJobs_resolves_the_sync_discography_artist_handler()
	{
		using var scope = BuildProvider().CreateScope();

		var handler = scope.ServiceProvider.GetRequiredService<ISyncArtistWithDiscographyJobHandler>();
		var resolver = scope.ServiceProvider.GetRequiredService<IDiscographyArtistResolver>();

		Assert.IsType<SyncArtistWithDiscographyJobHandler>(handler);
		Assert.IsType<DiscographyArtistResolver>(resolver);
	}

	[Fact]
	public void AddJobs_resolves_the_sync_all_artists_handler()
	{
		using var scope = BuildProvider().CreateScope();

		var handler = scope.ServiceProvider.GetRequiredService<ISyncAllArtistsWithDiscographyJobHandler>();

		Assert.IsType<SyncAllArtistsWithDiscographyJobHandler>(handler);
	}

	private static ServiceProvider BuildProvider()
	{
		return new ServiceCollection()
			.AddSingleton(Mock.Of<ICatalogue>())
			.AddSingleton(Mock.Of<ILibrarySource>())
			.AddSingleton(Mock.Of<IDiscographySource>())
			.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
			.AddJobs()
			.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
	}
}
