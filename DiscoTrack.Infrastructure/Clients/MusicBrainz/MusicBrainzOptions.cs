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

	[Range(0, 10)]
	public int BusyRetries { get; set; } = 3;

	public TimeSpan BusyRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

	[Range(1, 100)]
	public int PageSize { get; set; } = 100;

	[Range(1, 100)]
	public int SearchSize { get; set; } = 25;

	[Range(1, 100)]
	public int MaxReleaseGroupPages { get; set; } = 10;

	[Range(1, 10)]
	public int MaxRecordingsPerIsrc { get; set; } = 3;
}
