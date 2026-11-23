namespace DiscoTrack.Discography.SyncAlbumWithDiscography;

public interface ISyncAlbumWithDiscographyError;

public record UnableToConnectToCatalogueError(string Message) : ISyncAlbumWithDiscographyError;
public record UnableToFindAlbumError() : ISyncAlbumWithDiscographyError;
public record UnableToConnectToDiscographyError(string Message) : ISyncAlbumWithDiscographyError;
public record UnableToFindDiscographyAlbumError() : ISyncAlbumWithDiscographyError;
public record UnableToSaveArtistError(string Message) : ISyncAlbumWithDiscographyError;
public record UnableToSaveAlbumError(string Message) : ISyncAlbumWithDiscographyError;
