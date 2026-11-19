using System.ComponentModel.DataAnnotations;

namespace DiscoTrack.Infrastructure.Clients.MusicBrainz;

public class MusicBrainzOptions
{
	public const string Section = "MusicBrainz";

	[Required]
	public required string Contact { get; set; }

	public string BaseUrl { get; set; } = "https://musicbrainz.org/ws/2/";
	public string ApplicationName { get; set; } = "DiscoTrack";
	public TimeSpan MinRequestInterval { get; set; } = TimeSpan.FromSeconds(1);

	[Range(1, 100)]
	public int PageSize { get; set; } = 100;

	[Range(1, 100)]
	public int SearchSize { get; set; } = 25;
}
