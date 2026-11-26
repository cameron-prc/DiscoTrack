using DiscoTrack.Catalogue;
using DiscoTrack.Discography;
using DiscoTrack.Infrastructure.Catalogue;
using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Discography;
using DiscoTrack.Infrastructure.Jobs;
using DiscoTrack.Infrastructure.Library;
using DiscoTrack.Infrastructure.Listening;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Tokens;
using DiscoTrack.Jobs;
using DiscoTrack.Library;
using DiscoTrack.Listening;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

	public static IServiceCollection AddMusicBrainzDiscographySource(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<MusicBrainzOptions>()
			.Bind(configuration.GetSection(MusicBrainzOptions.Section))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<IMusicBrainzRateLimiter, MusicBrainzRateLimiter>();
		services.AddTransient<MusicBrainzRateLimitHandler>();
		services.AddHttpClient<IMusicBrainzClient, MusicBrainzClient>((provider, http) =>
			{
				var options = provider.GetRequiredService<IOptions<MusicBrainzOptions>>().Value;
				var version = typeof(DependencyInjection).Assembly.GetName().Version?.ToString(3);

				http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
				http.DefaultRequestHeaders.UserAgent.ParseAdd($"{options.ApplicationName}/{version} ({options.Contact})");
			})
			.AddHttpMessageHandler<MusicBrainzRateLimitHandler>();
		services.AddScoped<IDiscographySource, MusicBrainzDiscographySource>();
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

	public static IServiceCollection AddProviderTokens(this IServiceCollection services, IConfiguration configuration)
	{
		var tokenProtection = configuration.GetSection(TokenProtectionOptions.Section).Get<TokenProtectionOptions>() ?? new TokenProtectionOptions();

		services.AddDataProtection()
			.SetApplicationName("DiscoTrack")
			.PersistKeysToFileSystem(new DirectoryInfo(tokenProtection.KeysPath));
		services.AddScoped<IProviderTokenStore, SqliteProviderTokenStore>();
		return services;
	}

	public static IServiceCollection AddSpotifyListening(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<SpotifyOptions>()
			.Bind(configuration.GetSection(SpotifyOptions.Section));
		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<IPendingAuthorisations, PendingAuthorisations>();
		services.AddHttpClient<ISpotifyAccountsClient, SpotifyAccountsClient>((provider, http) =>
			http.BaseAddress = new Uri(provider.GetRequiredService<IOptions<SpotifyOptions>>().Value.AccountsBaseUrl.TrimEnd('/') + "/"));
		services.AddScoped<SpotifyConnection>();
		services.AddScoped<IListeningConnection>(provider => provider.GetRequiredService<SpotifyConnection>());
		services.AddScoped<ISpotifyAccessTokens>(provider => provider.GetRequiredService<SpotifyConnection>());
		return services;
	}

	public static IServiceCollection AddInMemoryJobQueue(this IServiceCollection services)
	{
		services.AddSingleton<IJobQueue, InMemoryJobQueue>();
		return services;
	}

	public static IServiceCollection AddJobBackgroundService(this IServiceCollection services)
	{
		services.AddHostedService<JobBackgroundService>();
		return services;
	}
}
