using DiscoTrack.Albums;

namespace DiscoTrack.Jobs.JobHandlers;

public interface ISyncAlbumWithDiscographyJobHandler
{
	Task Execute(AlbumId albumId, CancellationToken ct);
}
