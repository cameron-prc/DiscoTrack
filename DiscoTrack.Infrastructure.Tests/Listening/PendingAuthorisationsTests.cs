using DiscoTrack.Infrastructure.Listening;
using Microsoft.Extensions.Time.Testing;

namespace DiscoTrack.Infrastructure.Tests.Listening;

public class PendingAuthorisationsTests
{
	private readonly FakeTimeProvider _time = new();

	[Fact]
	public void Complete_accepts_the_state_that_was_started_once()
	{
		var pending = new PendingAuthorisations(_time);
		var state = pending.Start();

		Assert.True(pending.Complete(state));
		Assert.False(pending.Complete(state));
	}

	[Fact]
	public void Complete_rejects_another_state_and_keeps_the_started_one()
	{
		var pending = new PendingAuthorisations(_time);
		var state = pending.Start();

		Assert.False(pending.Complete("someone-elses-state"));
		Assert.True(pending.Complete(state));
	}

	[Fact]
	public void Complete_rejects_any_state_when_nothing_was_started()
	{
		Assert.False(new PendingAuthorisations(_time).Complete("state"));
	}

	[Fact]
	public void Complete_rejects_a_state_started_more_than_ten_minutes_ago()
	{
		var pending = new PendingAuthorisations(_time);
		var state = pending.Start();
		_time.Advance(TimeSpan.FromMinutes(10));

		Assert.False(pending.Complete(state));
	}

	[Fact]
	public void Start_replaces_the_state_started_before_and_never_repeats_one()
	{
		var pending = new PendingAuthorisations(_time);
		var first = pending.Start();
		var second = pending.Start();

		Assert.NotEqual(first, second);
		Assert.False(pending.Complete(first));
		Assert.True(pending.Complete(second));
	}
}
