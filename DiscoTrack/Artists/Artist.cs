using DiscoTrack.ExternalIds;

namespace DiscoTrack.Artists;

public class Artist
{
	public ArtistId Id { get; init; } = ArtistId.New();
	public IReadOnlyList<ExternalId> ExternalIds { get; set; } = [];
	public required string Name { get; set; }

	public void SetExternalId(ExternalId externalId)
	{
		ExternalIds = ExternalIds
			.Where(existing => existing.Provider != externalId.Provider)
			.Append(externalId)
			.ToList();
	}

	public override string ToString()
	{
		return $"{Name} ({Id})";
	}
}
