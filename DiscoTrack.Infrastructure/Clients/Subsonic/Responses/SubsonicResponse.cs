namespace DiscoTrack.Infrastructure.Clients.Subsonic.Responses;

internal abstract class SubsonicResponse
{
	public required string Status { get; init; }
	public SubsonicError? Error { get; init; }
}
