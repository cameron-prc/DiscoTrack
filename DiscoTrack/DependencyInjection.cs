using DiscoTrack.Discography;
using DiscoTrack.Discography.SyncAlbumWithDiscography;
using DiscoTrack.Discography.SyncArtistWithDiscography;
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
		services.AddScoped<IDiscographyAlbumResolver, DiscographyAlbumResolver>();
		services.AddScoped<ISyncAlbumWithDiscographyService,SyncAlbumWithDiscographyService>();
		services.AddScoped<ISyncAlbumWithDiscographyJobHandler, SyncAlbumWithDiscographyJobHandler>();
		services.AddScoped<IDiscographyArtistResolver, DiscographyArtistResolver>();
		services.AddScoped<ISyncArtistWithDiscographyService, SyncArtistWithDiscographyService>();
		services.AddScoped<ISyncArtistWithDiscographyJobHandler, SyncArtistWithDiscographyJobHandler>();
		services.AddScoped<ISyncAllArtistsWithDiscographyJobHandler, SyncAllArtistsWithDiscographyJobHandler>();
		services.AddScoped<IJobRunner, JobRunner>();
		return services;
	}
}
