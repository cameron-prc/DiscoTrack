using DiscoTrack.Core.Library;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Clients.Subsonic.Models;

namespace DiscoTrack.Infrastructure.Library;

internal static class Extensions
{
	public static LibraryAlbum ToLibraryAlbum(this AlbumID3 album)
	{
		return new(album.Id, album.Name, album.Year, album.IsCompilation, album.Credits().Select(a => a.ToLibraryArtist()).ToList());
	}

	public static LibraryArtist ToLibraryArtist(this ArtistID3 artist)
	{
		var musicBrainzId = string.IsNullOrEmpty(artist.MusicBrainzId) ? null : artist.MusicBrainzId;

		return new(artist.Id, artist.Name, musicBrainzId);
	}
}
