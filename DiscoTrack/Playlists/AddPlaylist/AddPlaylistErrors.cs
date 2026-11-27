namespace DiscoTrack.Playlists.AddPlaylist;

public interface IAddPlaylistError;

public record UnrecognisedPlaylistReferenceError : IAddPlaylistError;

public record UnableToFindPlaylistAtSourceError : IAddPlaylistError;

public record UnableToReadPlaylistSourceError(string Message) : IAddPlaylistError;

public record UnableToReadPlaylistsError(string Message) : IAddPlaylistError;

public record UnableToSavePlaylistError(string Message) : IAddPlaylistError;
