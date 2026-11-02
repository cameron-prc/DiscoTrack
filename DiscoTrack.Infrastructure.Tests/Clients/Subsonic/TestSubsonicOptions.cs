using DiscoTrack.Infrastructure.Clients.Subsonic;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Tests.Clients.Subsonic;

public static class TestSubsonicOptions
{
	public const string BaseUrl = "http://subsonic.test/";
	public const string Username = "testuser";
	public const string Salt = "c19b2d";
	public const string Token = "26719a1196d2a940705a59634eb18eab";

	public static IOptions<SubsonicOptions> Create(int pageSize = 500)
	{
		return Options.Create(new SubsonicOptions
		{
			BaseUrl = BaseUrl,
			Username = Username,
			Salt = Salt,
			Token = Token,
			PageSize = pageSize,
		});
	}
}
