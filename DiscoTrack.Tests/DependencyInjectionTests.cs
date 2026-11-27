using DiscoTrack.Catalogue;
using DiscoTrack.Discography;
using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using DiscoTrack.Library;
using DiscoTrack.ListenHistory;
using DiscoTrack.ListenHistory.ResolvePlays;
using DiscoTrack.ListenHistory.SyncListenHistory;
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

	[Fact]
	public void AddJobs_resolves_the_sync_listen_history_handler_and_service()
	{
		using var scope = BuildProvider().CreateScope();

		var handler = scope.ServiceProvider.GetRequiredService<ISyncListenHistoryJobHandler>();
		var service = scope.ServiceProvider.GetRequiredService<ISyncListenHistoryService>();

		Assert.IsType<SyncListenHistoryJobHandler>(handler);
		Assert.IsType<SyncListenHistoryService>(service);
		Assert.IsType<ResolvePlaysService>(scope.ServiceProvider.GetRequiredService<IResolvePlaysService>());
	}

	private static ServiceProvider BuildProvider()
	{
		return new ServiceCollection()
			.AddSingleton(Mock.Of<ICatalogue>())
			.AddSingleton(Mock.Of<ILibrarySource>())
			.AddSingleton(Mock.Of<IDiscographySource>())
			.AddSingleton(Mock.Of<IListenHistorySource>())
			.AddSingleton(Mock.Of<IListenHistory>())
			.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
			.AddJobs()
			.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
	}
}
