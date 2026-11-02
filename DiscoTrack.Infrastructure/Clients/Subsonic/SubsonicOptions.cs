using System.ComponentModel.DataAnnotations;

namespace DiscoTrack.Infrastructure.Clients.Subsonic;

public class SubsonicOptions
{
	public const string Section = "Subsonic";

	public required string BaseUrl { get; set; }
	public required string Username { get; set; }
	public required string Salt { get; set; }
	public required string Token { get; set; }

	[Range(1, int.MaxValue)]
	public int PageSize { get; set; } = 500;

	public string ApiVersion { get; set; } = "1.16.1";
	public string ClientName { get; set; } = "DiscoTrack";
}
