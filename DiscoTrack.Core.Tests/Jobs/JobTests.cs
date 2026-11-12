using DiscoTrack.Core.Jobs;

namespace DiscoTrack.Core.Tests.Jobs;

public class JobTests
{
	[Fact]
	public void A_new_job_is_pending_with_no_start_or_finish()
	{
		var job = new Job(JobType.SyncLibrary);

		Assert.NotEqual(Guid.Empty, job.Id);
		Assert.Equal(JobType.SyncLibrary, job.Type);
		Assert.Equal(JobStatus.Pending, job.Status);
		Assert.Equal(job.CreatedAt, job.UpdatedAt);
		Assert.Null(job.StartedAt);
		Assert.Null(job.FinishedAt);
	}

	[Fact]
	public void Each_job_gets_its_own_id()
	{
		Assert.NotEqual(new Job(JobType.SyncLibrary).Id, new Job(JobType.SyncLibrary).Id);
	}
}
