using DiscoTrack.Core.ExternalIds;
using DiscoTrack.Core.Library;

namespace DiscoTrack.Core.Tests.Library;

internal static class LibraryValues
{
	public static readonly LibraryArtist TheChecks = new(new SubsonicId("art-checks"), "The Checks");
	public static readonly LibraryArtist Beck = new(new SubsonicId("art-beck"), "Beck");
	public static readonly LibraryArtist McBaise = new(new SubsonicId("art-mcbaise"), "McBaise");

	public static LibraryAlbum Album(string subsonicId, string title, int? year, params LibraryArtist[] artists)
	{
		return new LibraryAlbum(new SubsonicId(subsonicId), title, year, IsCompilation: false, artists);
	}
}
