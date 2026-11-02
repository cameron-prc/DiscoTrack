using DiscoTrack.Core.Library;
using DiscoTrack.Infrastructure.Clients.Subsonic.Models;

namespace DiscoTrack.Infrastructure.Clients.Subsonic;

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

	public static string ToQueryValue(this AlbumListType type)
	{
		return char.ToLowerInvariant(type.ToString()[0]) + type.ToString()[1..];
	}

	public static IReadOnlyList<ArtistID3> Credits(this AlbumID3 album)
	{
		if (album.Artists is { Count: > 0 })
		{
			return album.Artists;
		}

		if (album.ArtistId is not null && album.Artist is not null)
		{
			return [new ArtistID3 { Id = album.ArtistId, Name = album.Artist }];
		}

		return [];
	}
}
