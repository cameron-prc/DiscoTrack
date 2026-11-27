using DiscoTrack.Common;

namespace DiscoTrack.Providers.ResolveProviderTracks;

public interface IResolveProviderTracksService
{
	Task<Either<IList<IResolveProviderTracksError>, ResolveProviderTracksResponse>> Run(IReadOnlyList<ProviderTrack> providerTracks, CancellationToken cancellationToken);
}
