namespace DiscoTrack.Playlists.SyncPlaylists;

public interface ISyncPlaylistsError;

public record UnableToReadPlaylistsError(string Message) : ISyncPlaylistsError;

public record UnableToReadPlaylistSourceError(string Message) : ISyncPlaylistsError;

public record UnableToSavePlaylistError(string Message) : ISyncPlaylistsError;
