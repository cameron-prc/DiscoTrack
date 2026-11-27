namespace DiscoTrack.Infrastructure.Clients.Spotify.Models;

/// <summary>One page of a list. Next is absent on the last page. Spotify can list an entry it can no longer describe as null.</summary>
public sealed record Page<T> where T : class
{
	public IReadOnlyList<T?> Items { get; init; } = [];
	public int Total { get; init; }
	public string? Next { get; init; }
}
