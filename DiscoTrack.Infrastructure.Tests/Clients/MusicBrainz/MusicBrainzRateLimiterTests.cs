using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using Microsoft.Extensions.Time.Testing;

namespace DiscoTrack.Infrastructure.Tests.Clients.MusicBrainz;

public class MusicBrainzRateLimiterTests
{
	private readonly FakeTimeProvider _time = new();

	private MusicBrainzRateLimiter RateLimiter()
	{
		return new(TestMusicBrainzOptions.Create(), _time);
	}

	[Fact]
	public void Wait_lets_the_first_request_through_immediately()
	{
		using var rateLimiter = RateLimiter();

		var wait = rateLimiter.Wait(CancellationToken.None);

		Assert.True(wait.IsCompletedSuccessfully);
	}

	[Fact]
	public async Task Wait_holds_a_second_request_for_the_rest_of_the_interval()
	{
		using var rateLimiter = RateLimiter();
		await rateLimiter.Wait(CancellationToken.None);
		_time.Advance(TimeSpan.FromMilliseconds(400));

		var wait = rateLimiter.Wait(CancellationToken.None);

		_time.Advance(TimeSpan.FromMilliseconds(599));
		Assert.False(wait.IsCompleted);
		_time.Advance(TimeSpan.FromMilliseconds(1));
		await wait.WaitAsync(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task Wait_does_not_hold_a_request_made_after_the_interval()
	{
		using var rateLimiter = RateLimiter();
		await rateLimiter.Wait(CancellationToken.None);
		_time.Advance(TimeSpan.FromSeconds(1));

		var wait = rateLimiter.Wait(CancellationToken.None);

		Assert.True(wait.IsCompletedSuccessfully);
	}

	[Fact]
	public async Task Wait_spaces_queued_requests_one_interval_apart()
	{
		using var rateLimiter = RateLimiter();
		await rateLimiter.Wait(CancellationToken.None);

		var second = rateLimiter.Wait(CancellationToken.None);
		var third = rateLimiter.Wait(CancellationToken.None);

		_time.Advance(TimeSpan.FromSeconds(1));
		await second.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.False(third.IsCompleted);
		_time.Advance(TimeSpan.FromSeconds(1));
		await third.WaitAsync(TimeSpan.FromSeconds(5));
	}
}
