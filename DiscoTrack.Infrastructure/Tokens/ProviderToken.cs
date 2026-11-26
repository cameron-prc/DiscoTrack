namespace DiscoTrack.Infrastructure.Tokens;

public record ProviderToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
