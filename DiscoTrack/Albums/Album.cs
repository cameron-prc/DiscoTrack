using DiscoTrack.Artists;
using DiscoTrack.ExternalIds;

namespace DiscoTrack.Albums;

public class Album
{
	public AlbumId Id { get; init; } = AlbumId.New();
	public IReadOnlyList<ExternalId> ExternalIds { get; set; } = [];
	public required string Title { get; set; }
	public required int Year { get; set; }
	public bool IsCompilation { get; set; }
	public IReadOnlyList<AlbumArtist> Artists { get; set; } = [];

	public Artist? PrimaryArtist()
		=> IsCompilation ? null : Artists.MinBy(a => a.Position)?.Artist;

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
		var artistName = IsCompilation ? "Various Artists" : PrimaryArtist()?.Name;

		return artistName is null
			? $"NoArtist - {Title} ({Year}) ({Id})"
			: $"{artistName} - {Title} ({Year}) ({Id})";
	}
}
