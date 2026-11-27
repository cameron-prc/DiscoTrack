using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Playlists;

namespace DiscoTrack.Jobs;

public class Job
{
	public Guid Id { get; }
	public JobStatus Status { get; private set; }
	public JobType Type { get; }
	public AlbumId? AlbumId { get; }
	public ArtistId? ArtistId { get; }
	public PlaylistId? PlaylistId { get; }

	public DateTime CreatedAt { get; }
	public DateTime UpdatedAt { get; private set; }
	public DateTime? StartedAt { get; private set; }
	public DateTime? FinishedAt { get; private set; }

	public Job(JobType jobType, AlbumId? albumId = null)
	{
		var now = DateTime.UtcNow;

		Id = Guid.CreateVersion7();
		Type = jobType;
		AlbumId = albumId;
		Status = JobStatus.Pending;

		CreatedAt = now;
		UpdatedAt = now;
	}

	public Job(JobType jobType, ArtistId artistId) : this(jobType)
	{
		ArtistId = artistId;
	}

	public Job(JobType jobType, PlaylistId playlistId) : this(jobType)
	{
		PlaylistId = playlistId;
	}

	public void MarkAsStarted()
	{
		var now = DateTime.UtcNow;

		Status = JobStatus.Running;
		StartedAt = now;
		UpdatedAt = now;
	}

	public void MarkAsCompleted()
	{
		var now = DateTime.UtcNow;

		Status = JobStatus.Completed;
		FinishedAt = now;
		UpdatedAt = now;
	}

	public void MarkAsFailed()
	{
		var now = DateTime.UtcNow;

		Status = JobStatus.Failed;
		FinishedAt = now;
		UpdatedAt = now;
	}

	public void MarkAsCancelled()
	{
		var now = DateTime.UtcNow;

		Status = JobStatus.Cancelled;
		FinishedAt = now;
		UpdatedAt = now;
	}
}
