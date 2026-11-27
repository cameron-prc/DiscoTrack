namespace DiscoTrack.Providers.ResolveProviderTracks;

public interface IResolveProviderTracksError;

public record UnableToReadCatalogueError(string Message) : IResolveProviderTracksError;
