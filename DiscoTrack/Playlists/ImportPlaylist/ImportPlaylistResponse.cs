using DiscoTrack.Albums;

namespace DiscoTrack.Playlists.ImportPlaylist;

public record ImportPlaylistResponse(Playlist Playlist, bool CopiedItems, int Items, int ResolvedItems, int UnresolvedItems, IReadOnlyList<Album> CreatedAlbums);
