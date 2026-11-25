using DiscoTrack.Artists;

namespace DiscoTrack.Jobs.JobHandlers;

public interface ISyncArtistWithDiscographyJobHandler
{
	Task Execute(ArtistId artistId, CancellationToken ct);
}
