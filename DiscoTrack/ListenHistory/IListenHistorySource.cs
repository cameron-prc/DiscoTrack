using DiscoTrack.ExternalIds;

namespace DiscoTrack.ListenHistory;

public interface IListenHistorySource
{
	Provider TrackProvider { get; }

	/// <summary>The plays the provider still remembers, or only those after the given instant.</summary>
	Task<IReadOnlyList<Play>> GetPlays(DateTimeOffset? after, CancellationToken ct);
}
