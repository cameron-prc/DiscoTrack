using System.Net;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Clients.MusicBrainz;

public class MusicBrainzRateLimitHandler(IMusicBrainzRateLimiter rateLimiter, IOptions<MusicBrainzOptions> options, TimeProvider timeProvider) : DelegatingHandler
{
	private readonly int _busyRetries = options.Value.BusyRetries;
	private readonly TimeSpan _busyRetryDelay = options.Value.BusyRetryDelay;

	/// <summary>Sends at the allowed rate. A request the server is too busy for is sent again, waiting longer each time.</summary>
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
	{
		for (var attempt = 1; ; attempt++)
		{
			await rateLimiter.Wait(ct);

			var response = await base.SendAsync(request, ct);

			if (response.StatusCode != HttpStatusCode.ServiceUnavailable || attempt > _busyRetries)
			{
				return response;
			}

			response.Dispose();
			await Task.Delay(_busyRetryDelay * attempt, timeProvider, ct);
		}
	}
}
