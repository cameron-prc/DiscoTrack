using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tests.Clients.MusicBrainz;

public static class TestMusicBrainzOptions
{
	public const string BaseUrl = "http://musicbrainz.test/ws/2/";
	public const string Contact = "https://discotrack.test/contact";

	public static IOptions<MusicBrainzOptions> Create(int pageSize = 100, int searchSize = 25)
	{
		return Options.Create(new MusicBrainzOptions
		{
			BaseUrl = BaseUrl,
			Contact = Contact,
			PageSize = pageSize,
			SearchSize = searchSize,
		});
	}
}
