using DiscoTrack.Providers;

namespace DiscoTrack.ListenHistory;

/// <summary>One listen, as the provider reported it. A track played at an instant is one play however often it is copied.</summary>
public record Play
{
	public PlayId Id { get; init; } = PlayId.New();
	public required DateTimeOffset PlayedAt { get; init; }
	public required ProviderTrack Track { get; init; }

	public sealed override string ToString()
	{
		return $"{Track.Title} at {PlayedAt:O} ({Id})";
	}
}
