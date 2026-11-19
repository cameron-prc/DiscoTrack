using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Clients.Subsonic.Models;
using DiscoTrack.Library;

namespace DiscoTrack.Infrastructure.Library;

internal static class Extensions
{
	public static LibraryAlbum ToLibraryAlbum(this AlbumID3 album)
	{
		var artists = album.Credits().Select(a => a.ToLibraryArtist()).ToList();

		return new(new SubsonicId(album.Id), album.Name, album.Year, album.IsCompilation, artists, ToMusicBrainzReleaseId(album.MusicBrainzId));
	}

	public static LibraryArtist ToLibraryArtist(this ArtistID3 artist)
	{
		return new(new SubsonicId(artist.Id), artist.Name, ToMusicBrainzArtistId(artist.MusicBrainzId));
	}

	private static MusicBrainzArtistId? ToMusicBrainzArtistId(string? value)
	{
		return string.IsNullOrEmpty(value) ? null : new MusicBrainzArtistId(value);
	}

	private static MusicBrainzReleaseId? ToMusicBrainzReleaseId(string? value)
	{
		return string.IsNullOrEmpty(value) ? null : new MusicBrainzReleaseId(value);
	}
}
