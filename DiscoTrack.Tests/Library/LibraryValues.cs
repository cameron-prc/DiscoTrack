using DiscoTrack.ExternalIds;
using DiscoTrack.Library;

namespace DiscoTrack.Tests.Library;

internal static class LibraryValues
{
	public static readonly LibraryArtist TheChecks = new(new SubsonicId("art-checks"), "The Checks");
	public static readonly LibraryArtist Beck = new(new SubsonicId("art-beck"), "Beck");
	public static readonly LibraryArtist McBaise = new(new SubsonicId("art-mcbaise"), "McBaise");

	public static LibraryAlbum Album(string subsonicId, string title, int? year, params LibraryArtist[] artists)
	{
		return new LibraryAlbum(new SubsonicId(subsonicId), title, year, IsCompilation: false, artists);
	}

	public static LibraryTrack Track(string subsonicId, string albumSubsonicId, int discNumber, int trackNumber, string title, params LibraryArtist[] artists)
	{
		return new LibraryTrack(new SubsonicId(subsonicId), new SubsonicId(albumSubsonicId), title, discNumber, trackNumber, DurationSeconds: null, artists);
	}
}
