namespace DiscoTrack.Infrastructure.Clients.Spotify.Responses;

internal sealed class TokenError
{
	public string? Error { get; init; }
	public string? ErrorDescription { get; init; }
}
