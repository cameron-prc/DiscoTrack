using DiscoTrack.ExternalIds;

namespace DiscoTrack.Artists;

public class Artist
{
	public ArtistId Id { get; init; } = ArtistId.New();
	public IReadOnlyList<ExternalId> ExternalIds { get; set; } = [];
	public required string Name { get; set; }
	public bool Tracked { get; set; }
	public IReadOnlyDictionary<Provider, DateTimeOffset> LastScannedAt { get; set; } = new Dictionary<Provider, DateTimeOffset>();

	public void SetExternalId(ExternalId externalId)
	{
		SetExternalIds([externalId]);
	}

	public void SetExternalIds(IReadOnlyCollection<ExternalId> externalIds)
	{
		var providers = externalIds.Select(externalId => externalId.Provider).ToHashSet();

		ExternalIds = ExternalIds
			.Where(existing => !providers.Contains(existing.Provider) || externalIds.Contains(existing))
			.Concat(externalIds.Except(ExternalIds))
			.ToList();
	}

	public void SetLastScannedAt(Provider provider, DateTimeOffset scannedAt)
	{
		LastScannedAt = LastScannedAt
			.Where(scan => scan.Key != provider)
			.Append(KeyValuePair.Create(provider, scannedAt))
			.ToDictionary();
	}

	public void RemoveExternalIds(Provider provider)
	{
		ExternalIds = ExternalIds
			.Where(existing => existing.Provider != provider)
			.ToList();
	}

	public override string ToString()
	{
		return $"{Name} ({Id})";
	}
}
