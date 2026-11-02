namespace DiscoTrack.Infrastructure.Clients.Subsonic.Responses;

internal sealed class SubsonicError
{
	public int Code { get; init; }
	public string? Message { get; init; }
}
