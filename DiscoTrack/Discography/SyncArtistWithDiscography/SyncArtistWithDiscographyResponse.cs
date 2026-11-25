using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Discography.SyncArtistWithDiscography;

public record SyncArtistWithDiscographyResponse(
	Artist Artist,
	IReadOnlyList<Album> SyncedAlbums,
	IReadOnlyList<Album> UnsyncedAlbums,
	IReadOnlyList<Album> CreatedAlbums,
	IReadOnlyList<Artist> CreatedArtists,
	IReadOnlyList<DiscographyAlbum> SkippedAlbums);
