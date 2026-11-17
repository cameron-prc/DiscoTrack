namespace DiscoTrack.Jobs.JobHandlers;

public interface IJobHandler
{
	Task Execute(CancellationToken ct);
}
