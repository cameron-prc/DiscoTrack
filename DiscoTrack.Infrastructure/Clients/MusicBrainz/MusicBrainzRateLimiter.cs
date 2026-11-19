using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Clients.MusicBrainz;

public sealed class MusicBrainzRateLimiter(IOptions<MusicBrainzOptions> options, TimeProvider timeProvider) : IMusicBrainzRateLimiter, IDisposable
{
	private readonly TimeSpan _minRequestInterval = options.Value.MinRequestInterval;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;

	public async Task Wait(CancellationToken ct)
	{
		await _gate.WaitAsync(ct);

		try
		{
			var remaining = _nextRequestAt - timeProvider.GetUtcNow();
			if (remaining > TimeSpan.Zero)
			{
				await Task.Delay(remaining, timeProvider, ct);
			}

			_nextRequestAt = timeProvider.GetUtcNow() + _minRequestInterval;
		}
		finally
		{
			_gate.Release();
		}
	}

	public void Dispose()
	{
		_gate.Dispose();
	}
}
