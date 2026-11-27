using DiscoTrack.Common;

namespace DiscoTrack.ListenHistory.ResolvePlays;

public interface IResolvePlaysService
{
	Task<Either<IList<IResolvePlaysError>, ResolvePlaysResponse>> Run(IReadOnlyList<Play> plays, CancellationToken cancellationToken);
}
