namespace DiscoTrack.ListenHistory.ResolvePlays;

public interface IResolvePlaysError;

public record UnableToReadCatalogueError(string Message) : IResolvePlaysError;
