using DiscoTrack.Core.Library;

namespace DiscoTrack.Core.Tests.Library;

internal static class LibraryValues
{
	public static readonly LibraryArtist TheChecks = new("art-checks", "The Checks");
	public static readonly LibraryArtist Beck = new("art-beck", "Beck");
	public static readonly LibraryArtist McBaise = new("art-mcbaise", "McBaise");

	public static LibraryAlbum Album(string subsonicId, string title, int? year, params LibraryArtist[] artists)
	{
		return new LibraryAlbum(subsonicId, title, year, IsCompilation: false, artists);
	}
}
