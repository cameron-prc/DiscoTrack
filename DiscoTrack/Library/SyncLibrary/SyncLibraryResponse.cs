using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Library.SyncLibrary;

public record SyncLibraryResponse(IList<Artist> NewArtists, IList<Album> NewAlbums, IList<LibraryAlbum> UnprocessedAlbums);
