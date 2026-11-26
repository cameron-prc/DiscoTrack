using DiscoTrack.Infrastructure.Tokens;

namespace DiscoTrack.Infrastructure.Clients.Spotify;

public class SpotifyAccessTokens(ISpotifyAccountsClient client, IProviderTokenStore tokenStore, TimeProvider timeProvider) : ISpotifyAccessTokens
{
	public const string Provider = "Spotify";

	private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

	public async Task<string> GetAccessToken(CancellationToken ct)
	{
		var token = await tokenStore.Get(Provider, ct)
			?? throw new SpotifyException("Spotify is not connected.");

		if (token.ExpiresAt > timeProvider.GetUtcNow() + RefreshMargin)
		{
			return token.AccessToken;
		}

		var response = await client.RefreshToken(token.RefreshToken, ct);
		var refreshed = new ProviderToken(
			response.AccessToken,
			string.IsNullOrEmpty(response.RefreshToken) ? token.RefreshToken : response.RefreshToken,
			timeProvider.GetUtcNow().AddSeconds(response.ExpiresIn));

		await tokenStore.Save(Provider, refreshed, ct);

		return refreshed.AccessToken;
	}
}
