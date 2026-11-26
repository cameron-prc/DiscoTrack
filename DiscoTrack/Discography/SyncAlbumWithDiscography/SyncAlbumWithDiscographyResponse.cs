using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Tracks;

namespace DiscoTrack.Discography.SyncAlbumWithDiscography;

public record SyncAlbumWithDiscographyResponse(
	Album Album,
	IReadOnlyList<Artist> CreatedArtists,
	IReadOnlyList<Artist> UpdatedArtists,
	IReadOnlyList<Artist> LinkedArtists,
	IReadOnlyList<Artist> UnlinkedArtists,
	Album? AbsorbedAlbum,
	IReadOnlyList<Track>? Tracks = null);
