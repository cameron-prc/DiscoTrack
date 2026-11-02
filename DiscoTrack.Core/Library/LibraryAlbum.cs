namespace DiscoTrack.Core.Library;

public record LibraryAlbum(
	string SubsonicId,
	string Title,
	int? Year,
	bool IsCompilation,
	IReadOnlyList<LibraryArtist> Artists);
