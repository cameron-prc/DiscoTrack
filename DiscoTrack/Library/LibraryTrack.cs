using DiscoTrack.ExternalIds;

namespace DiscoTrack.Library;

public record LibraryTrack(
	ExternalId Id,
	ExternalId AlbumId,
	string Title,
	int DiscNumber,
	int TrackNumber,
	int? DurationSeconds,
	IReadOnlyList<LibraryArtist> Artists,
	MusicBrainzRecordingId? MusicBrainzId = null)
{
	public IReadOnlyList<ExternalId> ExternalIds
		=> new ExternalId?[] { Id, MusicBrainzId }.OfType<ExternalId>().ToList();

	public override string ToString()
	{
		return $"{DiscNumber}.{TrackNumber} {Title} ({Id})";
	}
}
