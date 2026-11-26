using DiscoTrack.Albums;
using DiscoTrack.ExternalIds;

namespace DiscoTrack.Tracks;

public class Track
{
	public TrackId Id { get; init; } = TrackId.New();
	public required AlbumId AlbumId { get; init; }
	public IReadOnlyList<ExternalId> ExternalIds { get; set; } = [];
	public required string Title { get; set; }
	public required int DiscNumber { get; set; }
	public required int Position { get; set; }
	public int? DurationMs { get; set; }
	public IReadOnlyList<TrackArtist> Artists { get; set; } = [];

	public void SetExternalIds(IReadOnlyCollection<ExternalId> externalIds)
	{
		var providers = externalIds.Select(externalId => externalId.Provider).ToHashSet();

		ExternalIds = ExternalIds
			.Where(existing => !providers.Contains(existing.Provider) || externalIds.Contains(existing))
			.Concat(externalIds.Except(ExternalIds))
			.ToList();
	}

	public void RemoveExternalIds(Provider provider)
	{
		ExternalIds = ExternalIds
			.Where(existing => existing.Provider != provider)
			.ToList();
	}

	public override string ToString()
	{
		return $"{DiscNumber}.{Position} {Title} ({Id})";
	}
}
