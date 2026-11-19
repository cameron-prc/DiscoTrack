namespace DiscoTrack.Infrastructure.Clients.MusicBrainz;

public interface IMusicBrainzRateLimiter
{
	Task Wait(CancellationToken ct);
}
