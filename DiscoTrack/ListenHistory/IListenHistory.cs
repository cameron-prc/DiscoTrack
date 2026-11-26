using DiscoTrack.ExternalIds;

namespace DiscoTrack.ListenHistory;

public interface IListenHistory
{
	Task<DateTimeOffset?> GetLatestPlayedAt(Provider trackProvider, CancellationToken ct);

	/// <summary>Adds the plays it does not already hold and returns how many it added.</summary>
	Task<int> AddPlays(IReadOnlyList<Play> plays, CancellationToken ct);

	/// <summary>Plays from the most recent back.</summary>
	Task<IReadOnlyList<Play>> GetPlays(int offset, int limit, CancellationToken ct);

	Task<int> CountPlays(CancellationToken ct);
}
