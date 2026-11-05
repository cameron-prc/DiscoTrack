using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;
using DiscoTrack.Infrastructure.Catalogue.Dtos;

namespace DiscoTrack.Infrastructure.Catalogue;

internal static class Extensions
{
	public static Artist ToArtist(this ArtistDto artist)
	{
		return new Artist
		{
			Id = artist.Id,
			SubsonicId = artist.SubsonicId,
			Name = artist.Name,
		};
	}

	public static Album ToAlbum(this AlbumDto album, IReadOnlyList<AlbumArtist> artists)
	{
		return new Album
		{
			Id = album.Id,
			SubsonicId = album.SubsonicId,
			Title = album.Title,
			Year = album.Year,
			IsCompilation = album.IsCompilation,
			Artists = artists,
		};
	}

	public static AlbumArtist ToAlbumArtist(this AlbumArtistDto albumArtist)
	{
		return new AlbumArtist
		{
			Position = albumArtist.Position,
			Artist = new Artist { Id = albumArtist.ArtistId, SubsonicId = albumArtist.SubsonicId, Name = albumArtist.Name },
		};
	}

	public static ArtistDto ToDto(this Artist artist)
	{
		return new ArtistDto
		{
			Id = artist.Id,
			SubsonicId = artist.SubsonicId,
			Name = artist.Name,
		};
	}

	public static AlbumDto ToDto(this Album album)
	{
		return new AlbumDto
		{
			Id = album.Id,
			SubsonicId = album.SubsonicId,
			Title = album.Title,
			Year = album.Year,
			IsCompilation = album.IsCompilation,
		};
	}

	public static NewAlbumArtistDto ToDto(this AlbumArtist albumArtist, AlbumId albumId)
	{
		return new NewAlbumArtistDto
		{
			AlbumId = albumId,
			ArtistId = albumArtist.Artist.Id,
			Position = albumArtist.Position,
		};
	}
}
