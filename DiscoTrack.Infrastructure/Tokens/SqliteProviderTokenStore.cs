using System.Globalization;
using System.Security.Cryptography;
using Dapper;
using DiscoTrack.Infrastructure.Sqlite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tokens;

public class SqliteProviderTokenStore(IOptions<SqliteOptions> options, IDataProtectionProvider dataProtectionProvider) : IProviderTokenStore
{
	private const string Purpose = "DiscoTrack.ProviderTokens";

	private const string SelectToken = "SELECT AccessToken, RefreshToken, ExpiresAt FROM ProviderTokens WHERE Provider = @Provider";

	private const string UpsertToken = """
		INSERT INTO ProviderTokens (Provider, AccessToken, RefreshToken, ExpiresAt) VALUES (@Provider, @AccessToken, @RefreshToken, @ExpiresAt)
		ON CONFLICT (Provider) DO UPDATE SET AccessToken = excluded.AccessToken, RefreshToken = excluded.RefreshToken, ExpiresAt = excluded.ExpiresAt
		""";

	private const string DeleteToken = "DELETE FROM ProviderTokens WHERE Provider = @Provider";

	private readonly string _connectionString = options.Value.ConnectionString;
	private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

	public async Task<ProviderToken?> Get(string provider, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var row = await connection.QuerySingleOrDefaultAsync<TokenRow>(SelectToken, new { Provider = provider });
		if (row is null)
		{
			return null;
		}

		try
		{
			return new ProviderToken(
				_protector.Unprotect(row.AccessToken),
				_protector.Unprotect(row.RefreshToken),
				DateTimeOffset.Parse(row.ExpiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
		}
		catch (CryptographicException)
		{
			// The key ring that protected this token is gone, so the provider has to be connected again
			return null;
		}
	}

	public async Task Save(string provider, ProviderToken token, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		await connection.ExecuteAsync(UpsertToken, new TokenRow
		{
			Provider = provider,
			AccessToken = _protector.Protect(token.AccessToken),
			RefreshToken = _protector.Protect(token.RefreshToken),
			ExpiresAt = token.ExpiresAt.ToString("O", CultureInfo.InvariantCulture),
		});
	}

	public async Task Delete(string provider, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		await connection.ExecuteAsync(DeleteToken, new { Provider = provider });
	}

	private sealed class TokenRow
	{
		public string Provider { get; init; } = "";
		public string AccessToken { get; init; } = "";
		public string RefreshToken { get; init; } = "";
		public string ExpiresAt { get; init; } = "";
	}
}
