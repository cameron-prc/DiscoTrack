namespace DiscoTrack.Infrastructure.Clients.Spotify.Responses;

public sealed record TokenResponse
{
	public required string AccessToken { get; init; }
	public string? RefreshToken { get; init; }
	public int ExpiresIn { get; init; }
	public string? Scope { get; init; }
}
