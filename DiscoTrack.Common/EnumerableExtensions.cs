namespace DiscoTrack.Common;

public static class EnumerableExtensions
{
	// This goes against the design philosophy of linq, but sometimes it's convenient to add a
	// side effect to a linq chain
	public static IReadOnlyList<T> Apply<T>(this IEnumerable<T> source, Action<T> action)
	{
		var items = source.ToList();

		foreach (var item in items)
		{
			action(item);
		}

		return items;
	}
}
