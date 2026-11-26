namespace DiscoTrack.Infrastructure.Clients.Spotify;

public interface ISpotifyAccessTokens
{
	/// <summary>Returns an access token that is still valid, refreshing the stored one when it is about to expire.</summary>
	Task<string> GetAccessToken(CancellationToken ct);
}
