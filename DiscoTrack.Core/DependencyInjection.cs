using DiscoTrack.Core.Jobs;
using DiscoTrack.Core.Jobs.JobHandlers;
using DiscoTrack.Core.Library.SyncLibrary;
using Microsoft.Extensions.DependencyInjection;

namespace DiscoTrack.Core;

public static class DependencyInjection
{
	public static IServiceCollection AddJobs(this IServiceCollection services)
	{
		services.AddScoped<SyncLibrary>();
		services.AddScoped<ISyncLibraryHandler, SyncLibraryJobHandler>();
		services.AddScoped<IJobRunner, JobRunner>();
		return services;
	}
}
