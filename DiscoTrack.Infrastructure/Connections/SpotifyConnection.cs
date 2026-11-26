using DiscoTrack.Common;
using DiscoTrack.Connections;
using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Clients.Spotify.Responses;
using DiscoTrack.Infrastructure.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Connections;

public class SpotifyConnection(
	ISpotifyAccountsClient client,
	IProviderTokenStore tokenStore,
	IPendingAuthorisations pendingAuthorisations,
	IOptions<SpotifyOptions> options,
	TimeProvider timeProvider,
	ILogger<SpotifyConnection> logger) : IProviderConnection
{
	private const string Provider = SpotifyAccessTokens.Provider;

	// Every scope a feature built on this connection needs
	private static readonly IReadOnlyList<string> Scopes = ["user-read-recently-played"];

	private readonly SpotifyOptions _options = options.Value;

	public bool IsConfigured
	{
		get { return _options.IsConfigured; }
	}

	public async Task<bool> IsConnected(CancellationToken ct)
	{
		return await tokenStore.Get(Provider, ct) is not null;
	}

	public string StartAuthorisation()
	{
		var query = new Dictionary<string, string>
		{
			["client_id"] = _options.ClientId,
			["response_type"] = "code",
			["redirect_uri"] = _options.RedirectUri,
			["scope"] = string.Join(" ", Scopes),
			["state"] = pendingAuthorisations.Start(),
		};

		var queryString = string.Join("&", query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
		return $"{_options.AccountsBaseUrl.TrimEnd('/')}/{SpotifyRoutes.Authorise}?{queryString}";
	}

	public async Task<Either<IList<IConnectProviderError>, ProviderConnected>> CompleteAuthorisation(string? code, string? state, string? error, CancellationToken ct)
	{
		if (state is null || !pendingAuthorisations.Complete(state))
		{
			return new List<IConnectProviderError> { new AuthorisationNotStartedHereError() };
		}

		if (error is not null || string.IsNullOrEmpty(code))
		{
			return new List<IConnectProviderError> { new AuthorisationDeniedError(error ?? "Spotify sent no code") };
		}

		TokenResponse response;

		try
		{
			response = await client.ExchangeCode(code, ct);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to exchange the Spotify authorisation code");

			return new List<IConnectProviderError> { new UnableToExchangeCodeError(exception.Message) };
		}

		if (string.IsNullOrEmpty(response.RefreshToken))
		{
			return new List<IConnectProviderError> { new UnableToExchangeCodeError("Spotify sent no refresh token") };
		}

		var token = new ProviderToken(response.AccessToken, response.RefreshToken, timeProvider.GetUtcNow().AddSeconds(response.ExpiresIn));

		try
		{
			await tokenStore.Save(Provider, token, ct);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save the Spotify token");

			return new List<IConnectProviderError> { new UnableToSaveTokenError(exception.Message) };
		}

		return new ProviderConnected(token.ExpiresAt);
	}

	public Task Disconnect(CancellationToken ct)
	{
		return tokenStore.Delete(Provider, ct);
	}
}
