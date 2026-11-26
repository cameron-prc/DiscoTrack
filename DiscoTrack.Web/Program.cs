using DiscoTrack;
using DiscoTrack.Infrastructure;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Sqlite.Migrations;
using DiscoTrack.Connections;
using DiscoTrack.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
	.AddSqlite(builder.Configuration)
	.AddSqliteCatalogue()
	.AddSubsonicLibrarySource(builder.Configuration)
	.AddMusicBrainzDiscographySource(builder.Configuration)
	.AddProviderTokens(builder.Configuration)
	.AddSpotifyConnection(builder.Configuration)
	.AddJobs()
	.AddInMemoryJobQueue()
	.AddJobBackgroundService();

var app = builder.Build();

Migrator.Migrate(builder.Configuration.GetConnectionString(SqliteOptions.ConnectionStringName)!);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapGet("/oauth/spotify/callback", async (string? code, string? state, string? error, IProviderConnection connection, CancellationToken ct) =>
{
	var result = await connection.CompleteAuthorisation(code, state, error, ct);

	return Results.Redirect(result.IsRight ? "/spotify" : $"/spotify?failure={Uri.EscapeDataString(result.GetLeft[0].GetType().Name)}");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
