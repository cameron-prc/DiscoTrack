namespace DiscoTrack.Discography.SyncArtistWithDiscography;

public interface ISyncArtistWithDiscographyError;

public record UnableToConnectToCatalogueError(string Message) : ISyncArtistWithDiscographyError;
public record UnableToFindArtistError() : ISyncArtistWithDiscographyError;
public record UnableToConnectToDiscographyError(string Message) : ISyncArtistWithDiscographyError;
public record UnableToFindDiscographyArtistError() : ISyncArtistWithDiscographyError;
public record UnableToSaveArtistError(string Message) : ISyncArtistWithDiscographyError;
public record UnableToSaveAlbumError(string Message) : ISyncArtistWithDiscographyError;
