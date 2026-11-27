namespace DiscoTrack.Playlists.ImportPlaylist;

public interface IImportPlaylistError;

public record UnableToReadPlaylistError(string Message) : IImportPlaylistError;

public record UnableToFindPlaylistError : IImportPlaylistError;

public record UnableToReadPlaylistItemsFromSourceError(string Message) : IImportPlaylistError;

public record UnableToSavePlaylistItemsError(string Message) : IImportPlaylistError;

public record UnableToResolvePlaylistItemsError(string Message) : IImportPlaylistError;
