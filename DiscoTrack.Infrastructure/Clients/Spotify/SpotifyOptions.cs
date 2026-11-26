namespace DiscoTrack.Infrastructure.Clients.Spotify;

public class SpotifyOptions
{
	public const string Section = "Spotify";

	public string ClientId { get; set; } = "";
	public string ClientSecret { get; set; } = "";

	/// <summary>Must match a redirect address registered for the Spotify app exactly.</summary>
	public string RedirectUri { get; set; } = "";

	public string AccountsBaseUrl { get; set; } = "https://accounts.spotify.com/";
	public string ApiBaseUrl { get; set; } = "https://api.spotify.com/v1/";

	public bool IsConfigured
		=> ClientId.Length > 0 && ClientSecret.Length > 0 && RedirectUri.Length > 0;
}
