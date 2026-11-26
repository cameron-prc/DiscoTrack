namespace DiscoTrack.Infrastructure.Clients.Spotify.Responses;

internal sealed class ApiError
{
	public ApiErrorDetail? Error { get; init; }
}

internal sealed class ApiErrorDetail
{
	public string? Message { get; init; }
}
