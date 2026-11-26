using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Infrastructure.Sqlite.Migrations;
using DiscoTrack.Infrastructure.Tests.Sqlite;
using DiscoTrack.Infrastructure.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tests.Tokens;

public sealed class SqliteProviderTokenStoreTests : IDisposable
{
	private static readonly DateTimeOffset ExpiresAt = new(2026, 11, 27, 9, 30, 0, TimeSpan.FromHours(12));
	private static readonly ProviderToken Token = new("access-token-value", "refresh-token-value", ExpiresAt);

	private readonly TestDatabase _db = new();
	private readonly SqliteProviderTokenStore _store;

	public SqliteProviderTokenStoreTests()
	{
		Migrator.Migrate(_db.ConnectionString);
		_store = Store(new EphemeralDataProtectionProvider());
	}

	public void Dispose()
	{
		_db.Dispose();
	}

	private SqliteProviderTokenStore Store(IDataProtectionProvider dataProtectionProvider)
	{
		return new SqliteProviderTokenStore(Options.Create(new SqliteOptions { ConnectionString = _db.ConnectionString }), dataProtectionProvider);
	}

	[Fact]
	public async Task Get_returns_null_when_the_provider_has_no_token()
	{
		Assert.Null(await _store.Get("Spotify", CancellationToken.None));
	}

	[Fact]
	public async Task Save_round_trips_the_token_and_its_expiry()
	{
		await _store.Save("Spotify", Token, CancellationToken.None);

		Assert.Equal(Token, await _store.Get("Spotify", CancellationToken.None));
	}

	[Fact]
	public async Task Save_does_not_write_the_tokens_as_they_are()
	{
		await _store.Save("Spotify", Token, CancellationToken.None);

		await using var connection = new SqliteConnection(_db.ConnectionString);
		await connection.OpenAsync();
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT AccessToken || '|' || RefreshToken FROM ProviderTokens";
		var stored = (string)(await command.ExecuteScalarAsync())!;

		Assert.DoesNotContain("access-token-value", stored);
		Assert.DoesNotContain("refresh-token-value", stored);
	}

	[Fact]
	public async Task Save_replaces_the_token_the_provider_had()
	{
		await _store.Save("Spotify", Token, CancellationToken.None);
		var refreshed = new ProviderToken("new-access", "refresh-token-value", ExpiresAt.AddHours(1));

		await _store.Save("Spotify", refreshed, CancellationToken.None);

		Assert.Equal(refreshed, await _store.Get("Spotify", CancellationToken.None));
	}

	[Fact]
	public async Task Tokens_are_kept_per_provider()
	{
		await _store.Save("Spotify", Token, CancellationToken.None);

		Assert.Null(await _store.Get("LastFm", CancellationToken.None));
	}

	[Fact]
	public async Task Delete_removes_the_token()
	{
		await _store.Save("Spotify", Token, CancellationToken.None);

		await _store.Delete("Spotify", CancellationToken.None);

		Assert.Null(await _store.Get("Spotify", CancellationToken.None));
	}

	[Fact]
	public async Task Get_returns_null_when_the_token_was_protected_by_another_key_ring()
	{
		await _store.Save("Spotify", Token, CancellationToken.None);

		var token = await Store(new EphemeralDataProtectionProvider()).Get("Spotify", CancellationToken.None);

		Assert.Null(token);
	}
}
