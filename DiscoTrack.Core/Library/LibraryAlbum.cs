using DiscoTrack.Core.ExternalIds;

namespace DiscoTrack.Core.Library;

public record LibraryAlbum(
	ExternalId Id,
	string Title,
	int? Year,
	bool IsCompilation,
	IReadOnlyList<LibraryArtist> Artists,
	MusicBrainzId? MusicBrainzId = null);
