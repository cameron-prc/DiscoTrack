using System.Net.Http.Headers;

namespace DiscoTrack.Infrastructure.Clients.Spotify;

public class SpotifyAuthorisationHandler(ISpotifyAccessTokens accessTokens) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await accessTokens.GetAccessToken(cancellationToken));

		return await base.SendAsync(request, cancellationToken);
	}
}
