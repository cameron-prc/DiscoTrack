namespace DiscoTrack.Core.Jobs.JobHandlers;

public interface IJobHandler
{
	Task Execute(CancellationToken ct);
}
