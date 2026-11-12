using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;

namespace DiscoTrack.Core.Library.SyncLibrary;

public record SyncLibraryResponse(IList<Artist> NewArtists, IList<Album> NewAlbums, IList<LibraryAlbum> UnprocessedAlbums);
