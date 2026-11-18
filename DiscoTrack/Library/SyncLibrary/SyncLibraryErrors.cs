namespace DiscoTrack.Library.SyncLibrary;

public interface ISyncLibraryError;

public record FailedToReadCatalogueError(string Message) : ISyncLibraryError;

public record FailedToReadLibraryError(string Message) : ISyncLibraryError;
