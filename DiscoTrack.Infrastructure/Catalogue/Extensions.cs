using System.Globalization;
using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Catalogue.Dtos;

namespace DiscoTrack.Infrastructure.Catalogue;

internal static class Extensions
{
	public static Artist ToArtist(this ArtistDto artist, ILookup<ArtistId, ExternalId> externalIds, ILookup<ArtistId, KeyValuePair<Provider, DateTimeOffset>> scans)
	{
		return new Artist
		{
			Id = artist.Id,
			ExternalIds = externalIds[artist.Id].ToList(),
			Name = artist.Name,
			Tracked = artist.Tracked,
			LastScannedAt = scans[artist.Id].ToDictionary(),
		};
	}

	public static Album ToAlbum(this AlbumDto album, IReadOnlyList<AlbumArtist> artists, ILookup<AlbumId, ExternalId> externalIds)
	{
		return new Album
		{
			Id = album.Id,
			ExternalIds = externalIds[album.Id].ToList(),
			Title = album.Title,
			Year = album.Year,
			IsCompilation = album.IsCompilation,
			Tracked = album.Tracked,
			Artists = artists,
		};
	}

	public static AlbumArtist ToAlbumArtist(this AlbumArtistDto albumArtist, ILookup<ArtistId, ExternalId> externalIds, ILookup<ArtistId, KeyValuePair<Provider, DateTimeOffset>> scans)
	{
		return new AlbumArtist
		{
			Position = albumArtist.Position,
			Artist = new Artist { Id = albumArtist.ArtistId, ExternalIds = externalIds[albumArtist.ArtistId].ToList(), Name = albumArtist.Name, Tracked = albumArtist.Tracked, LastScannedAt = scans[albumArtist.ArtistId].ToDictionary() },
		};
	}

	public static ILookup<ArtistId, ExternalId> ToLookup(this IEnumerable<ArtistExternalIdDto> externalIds)
	{
		return externalIds.ToLookup(externalId => externalId.ArtistId, externalId => ToExternalId(externalId.Provider, externalId.Value));
	}

	public static ILookup<AlbumId, ExternalId> ToLookup(this IEnumerable<AlbumExternalIdDto> externalIds)
	{
		return externalIds.ToLookup(externalId => externalId.AlbumId, externalId => ToExternalId(externalId.Provider, externalId.Value));
	}

	public static ILookup<ArtistId, KeyValuePair<Provider, DateTimeOffset>> ToLookup(this IEnumerable<ArtistScanDto> scans)
	{
		return scans.ToLookup(
			scan => scan.ArtistId,
			scan => KeyValuePair.Create(Enum.Parse<Provider>(scan.Provider), DateTimeOffset.Parse(scan.ScannedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
	}

	public static ArtistDto ToDto(this Artist artist)
	{
		return new ArtistDto
		{
			Id = artist.Id,
			Name = artist.Name,
			Tracked = artist.Tracked,
		};
	}

	public static AlbumDto ToDto(this Album album)
	{
		return new AlbumDto
		{
			Id = album.Id,
			Title = album.Title,
			Year = album.Year,
			IsCompilation = album.IsCompilation,
			Tracked = album.Tracked,
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

	public static ArtistExternalIdDto ToDto(this ExternalId externalId, ArtistId artistId)
	{
		return new ArtistExternalIdDto
		{
			ArtistId = artistId,
			Provider = externalId.Provider.ToString(),
			Value = externalId.Value,
		};
	}

	public static ArtistScanDto ToDto(this KeyValuePair<Provider, DateTimeOffset> scan, ArtistId artistId)
	{
		return new ArtistScanDto
		{
			ArtistId = artistId,
			Provider = scan.Key.ToString(),
			ScannedAt = scan.Value.ToString("O", CultureInfo.InvariantCulture),
		};
	}

	public static AlbumExternalIdDto ToDto(this ExternalId externalId, AlbumId albumId)
	{
		return new AlbumExternalIdDto
		{
			AlbumId = albumId,
			Provider = externalId.Provider.ToString(),
			Value = externalId.Value,
		};
	}

	private static ExternalId ToExternalId(string provider, string value)
	{
		return Enum.Parse<Provider>(provider) switch
		{
			Provider.Subsonic => new SubsonicId(value),
			Provider.MusicBrainzArtist => new MusicBrainzArtistId(value),
			Provider.MusicBrainzRelease => new MusicBrainzReleaseId(value),
			Provider.MusicBrainzReleaseGroup => new MusicBrainzReleaseGroupId(value),
			_ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
		};
	}
}
