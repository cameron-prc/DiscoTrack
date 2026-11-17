using DiscoTrack.Jobs;
using DiscoTrack.Jobs.JobHandlers;
using DiscoTrack.Library.SyncLibrary;
using Microsoft.Extensions.DependencyInjection;

namespace DiscoTrack;

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
