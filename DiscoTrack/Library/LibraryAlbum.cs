using DiscoTrack.ExternalIds;

namespace DiscoTrack.Library;

public record LibraryAlbum(
	ExternalId Id,
	string Title,
	int? Year,
	bool IsCompilation,
	IReadOnlyList<LibraryArtist> Artists,
	MusicBrainzId? MusicBrainzId = null)
{
	public IReadOnlyList<ExternalId> ExternalIds
		=> new ExternalId?[] { Id, MusicBrainzId }.OfType<ExternalId>().ToList();

	public override string ToString()
	{
		var artistName = IsCompilation ? "Various Artists" : Artists.FirstOrDefault()?.Name;
		var title = Year is null ? Title : $"{Title} ({Year})";

		return artistName is null
			? $"{title} ({Id})"
			: $"{artistName} - {title} ({Id})";
	}
}
