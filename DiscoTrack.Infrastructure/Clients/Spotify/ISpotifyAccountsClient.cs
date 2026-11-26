using DiscoTrack.Infrastructure.Clients.Spotify.Responses;

namespace DiscoTrack.Infrastructure.Clients.Spotify;

public interface ISpotifyAccountsClient
{
	Task<TokenResponse> ExchangeCode(string code, CancellationToken ct);
	Task<TokenResponse> RefreshToken(string refreshToken, CancellationToken ct);
}
