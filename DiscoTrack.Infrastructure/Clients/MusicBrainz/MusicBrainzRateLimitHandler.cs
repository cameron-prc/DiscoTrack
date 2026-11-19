namespace DiscoTrack.Infrastructure.Clients.MusicBrainz;

public class MusicBrainzRateLimitHandler(IMusicBrainzRateLimiter rateLimiter) : DelegatingHandler
{
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
	{
		await rateLimiter.Wait(ct);

		return await base.SendAsync(request, ct);
	}
}
