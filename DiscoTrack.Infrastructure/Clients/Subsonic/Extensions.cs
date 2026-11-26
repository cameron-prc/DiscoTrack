using DiscoTrack.Infrastructure.Clients.Subsonic.Models;

namespace DiscoTrack.Infrastructure.Clients.Subsonic;

internal static class Extensions
{
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

	public static IReadOnlyList<ArtistID3> Credits(this Child song)
	{
		if (song.Artists is { Count: > 0 })
		{
			return song.Artists;
		}

		if (song.ArtistId is not null && song.Artist is not null)
		{
			return [new ArtistID3 { Id = song.ArtistId, Name = song.Artist }];
		}

		return [];
	}
}
