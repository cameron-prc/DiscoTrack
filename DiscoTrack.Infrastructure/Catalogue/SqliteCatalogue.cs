using Dapper;
using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;
using DiscoTrack.Core.Catalogue;
using DiscoTrack.Infrastructure.Catalogue.Dtos;
using DiscoTrack.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Catalogue;

public class SqliteCatalogue(IOptions<SqliteOptions> options) : ICatalogue
{
	// TODO: These need to be moved into stored procs once confirmed
	private const string SelectArtists = "SELECT Id, SubsonicId, Name FROM Artists ORDER BY Name";

	private const string SelectAlbums = """
		SELECT al.Id, al.SubsonicId, al.Title, al.Year, al.IsCompilation,
		       aa.Position, ar.Id AS ArtistId, ar.SubsonicId, ar.Name
		FROM Albums al
		LEFT JOIN AlbumArtists aa ON aa.AlbumId = al.Id
		LEFT JOIN Artists ar ON ar.Id = aa.ArtistId
		ORDER BY al.Title, aa.Position
		""";

	private const string SelectArtistAlbums = """
		SELECT al.Id, al.SubsonicId, al.Title, al.Year, al.IsCompilation,
		       aa.Position, ar.Id AS ArtistId, ar.SubsonicId, ar.Name
		FROM Albums al
		JOIN AlbumArtists aa ON aa.AlbumId = al.Id
		JOIN Artists ar ON ar.Id = aa.ArtistId
		WHERE al.Id IN (SELECT AlbumId FROM AlbumArtists WHERE ArtistId = @ArtistId)
		ORDER BY al.Title, aa.Position
		""";

	private const string InsertArtist = "INSERT INTO Artists (Id, SubsonicId, Name) VALUES (@Id, @SubsonicId, @Name)";

	private const string InsertAlbum = "INSERT INTO Albums (Id, SubsonicId, Title, Year, IsCompilation) VALUES (@Id, @SubsonicId, @Title, @Year, @IsCompilation)";

	private const string InsertAlbumArtist = "INSERT INTO AlbumArtists (AlbumId, ArtistId, Position) VALUES (@AlbumId, @ArtistId, @Position)";

	private readonly string _connectionString = options.Value.ConnectionString;

	public async Task<IReadOnlyList<Artist>> GetArtists(CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var artists = await connection.QueryAsync<ArtistDto>(SelectArtists);

		return artists
			.Select(a => a.ToArtist())
			.ToList();
	}

	public async Task<IReadOnlyList<Album>> GetAlbums(CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var rows = await connection.QueryAsync<AlbumDto, AlbumArtistDto?, (AlbumDto Album, AlbumArtistDto? AlbumArtist)>(
			SelectAlbums,
			(album, albumArtist) => (album, albumArtist),
			splitOn: "Position");

		return rows
			.GroupBy(r => r.Album.Id)
			.Select(g =>
				g.First().Album.ToAlbum(
					g.Where(r => r.AlbumArtist is not null)
					.Select(r => r.AlbumArtist!.ToAlbumArtist())
					.ToList()
				)
			)
			.ToList();
	}

	public async Task<IReadOnlyList<Album>> GetArtistAlbums(ArtistId artistId, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var rows = await connection.QueryAsync<AlbumDto, AlbumArtistDto?, (AlbumDto Album, AlbumArtistDto? AlbumArtist)>(
			SelectArtistAlbums,
			(album, albumArtist) => (album, albumArtist),
			new { ArtistId = artistId },
			splitOn: "Position");

		return rows
			.GroupBy(r => r.Album.Id)
			.Select(g =>
				g.First().Album.ToAlbum(
					g.Select(r => r.AlbumArtist!.ToAlbumArtist())
					.ToList()
				)
			)
			.ToList();
	}

	public async Task AddArtists(IReadOnlyList<Artist> artists, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		await connection.ExecuteAsync(InsertArtist, artists.Select(a => a.ToDto()), transaction);

		await transaction.CommitAsync(ct);
	}

	public async Task AddAlbums(IReadOnlyList<Album> albums, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var albumDtos = albums.Select(album => album.ToDto());
		var albumArtistDtos = albums
			.SelectMany(album => album.Artists.Select(albumArtist => albumArtist.ToDto(album.Id)));

		await connection.ExecuteAsync(InsertAlbum, albumDtos, transaction);
		await connection.ExecuteAsync(InsertAlbumArtist, albumArtistDtos, transaction);

		await transaction.CommitAsync(ct);
	}
}
