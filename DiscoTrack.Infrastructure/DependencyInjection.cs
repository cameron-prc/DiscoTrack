using DiscoTrack.Core.Catalogue;
using DiscoTrack.Core.Library;
using DiscoTrack.Infrastructure.Catalogue;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Library;
using DiscoTrack.Infrastructure.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddSubsonicLibrarySource(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<SubsonicOptions>()
			.Bind(configuration.GetSection(SubsonicOptions.Section))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.AddHttpClient<ISubsonicClient, SubsonicClient>((provider, http) =>
			http.BaseAddress = new Uri(provider.GetRequiredService<IOptions<SubsonicOptions>>().Value.BaseUrl.TrimEnd('/') + "/"));
		services.AddScoped<ILibrarySource, SubsonicLibrarySource>();
		return services;
	}

	public static IServiceCollection AddSqlite(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<SqliteOptions>()
			.Configure(options => options.ConnectionString = configuration.GetConnectionString(SqliteOptions.ConnectionStringName)!)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return services;
	}

	public static IServiceCollection AddSqliteCatalogue(this IServiceCollection services)
	{
		services.AddScoped<ICatalogue, SqliteCatalogue>();
		return services;
	}
}
