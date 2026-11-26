using System.Security.Cryptography;

namespace DiscoTrack.Infrastructure.Listening;

public class PendingAuthorisations(TimeProvider timeProvider) : IPendingAuthorisations
{
	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

	private readonly Lock _lock = new();
	private (string State, DateTimeOffset ExpiresAt)? _pending;

	public string Start()
	{
		var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

		lock (_lock)
		{
			_pending = (state, timeProvider.GetUtcNow() + Lifetime);
		}

		return state;
	}

	public bool Complete(string state)
	{
		lock (_lock)
		{
			var pending = _pending;

			if (pending is null || !CryptographicOperations.FixedTimeEquals(Encode(pending.Value.State), Encode(state)))
			{
				return false;
			}

			_pending = null;

			return pending.Value.ExpiresAt > timeProvider.GetUtcNow();
		}
	}

	private static byte[] Encode(string value)
	{
		return System.Text.Encoding.UTF8.GetBytes(value);
	}
}
