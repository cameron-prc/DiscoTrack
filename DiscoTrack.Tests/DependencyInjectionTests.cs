using DiscoTrack.Catalogue;
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

	private static ServiceProvider BuildProvider()
	{
		return new ServiceCollection()
			.AddSingleton(Mock.Of<ICatalogue>())
			.AddSingleton(Mock.Of<ILibrarySource>())
			.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
			.AddJobs()
			.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
	}
}
