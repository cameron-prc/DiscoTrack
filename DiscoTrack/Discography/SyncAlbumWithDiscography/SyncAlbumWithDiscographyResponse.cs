using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Discography.SyncAlbumWithDiscography;

public record SyncAlbumWithDiscographyResponse(
	Album Album,
	IReadOnlyList<Artist> CreatedArtists,
	IReadOnlyList<Artist> UpdatedArtists,
	IReadOnlyList<Artist> LinkedArtists,
	IReadOnlyList<Artist> UnlinkedArtists);
