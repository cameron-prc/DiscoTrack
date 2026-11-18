using DiscoTrack.ExternalIds;

namespace DiscoTrack.Library;

public record LibraryArtist(ExternalId Id, string Name, MusicBrainzId? MusicBrainzId = null)
{
	public IReadOnlyList<ExternalId> ExternalIds
		=> new ExternalId?[] { Id, MusicBrainzId }.OfType<ExternalId>().ToList();

	public override string ToString()
	{
		return $"{Name} ({Id})";
	}
}
