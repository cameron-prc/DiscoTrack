using Dapper;
using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Infrastructure.Catalogue.Dtos;
using DiscoTrack.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Catalogue;

public class SqliteCatalogue(IOptions<SqliteOptions> options) : ICatalogue
{
	// TODO: These need to be moved into stored procs once confirmed
	private const string SelectArtists = "SELECT Id, Name FROM Artists ORDER BY Name";

	private const string SelectArtistExternalIds = "SELECT ArtistId, Provider, Value FROM ArtistExternalIds";

	private const string SelectAlbums = """
		SELECT al.Id, al.Title, al.Year, al.IsCompilation,
		       aa.Position, ar.Id AS ArtistId, ar.Name
		FROM Albums al
		LEFT JOIN AlbumArtists aa ON aa.AlbumId = al.Id
		LEFT JOIN Artists ar ON ar.Id = aa.ArtistId
		ORDER BY al.Title, aa.Position
		""";

	private const string SelectAlbumExternalIds = "SELECT AlbumId, Provider, Value FROM AlbumExternalIds";

	private const string SelectArtistAlbums = """
		SELECT al.Id, al.Title, al.Year, al.IsCompilation,
		       aa.Position, ar.Id AS ArtistId, ar.Name
		FROM Albums al
		JOIN AlbumArtists aa ON aa.AlbumId = al.Id
		JOIN Artists ar ON ar.Id = aa.ArtistId
		WHERE al.Id IN (SELECT AlbumId FROM AlbumArtists WHERE ArtistId = @ArtistId)
		ORDER BY al.Title, aa.Position
		""";

	private const string SelectArtistAlbumExternalIds = """
		SELECT AlbumId, Provider, Value
		FROM AlbumExternalIds
		WHERE AlbumId IN (SELECT AlbumId FROM AlbumArtists WHERE ArtistId = @ArtistId)
		""";

	private const string SelectArtistAlbumCreditExternalIds = """
		SELECT ArtistId, Provider, Value
		FROM ArtistExternalIds
		WHERE ArtistId IN (
			SELECT credit.ArtistId
			FROM AlbumArtists credit
			JOIN AlbumArtists own ON own.AlbumId = credit.AlbumId
			WHERE own.ArtistId = @ArtistId
		)
		""";

	private const string InsertArtist = "INSERT INTO Artists (Id, Name) VALUES (@Id, @Name)";

	private const string InsertArtistExternalId = "INSERT INTO ArtistExternalIds (ArtistId, Provider, Value) VALUES (@ArtistId, @Provider, @Value)";

	private const string InsertAlbum = "INSERT INTO Albums (Id, Title, Year, IsCompilation) VALUES (@Id, @Title, @Year, @IsCompilation)";

	private const string InsertAlbumExternalId = "INSERT INTO AlbumExternalIds (AlbumId, Provider, Value) VALUES (@AlbumId, @Provider, @Value)";

	private const string InsertAlbumArtist = "INSERT INTO AlbumArtists (AlbumId, ArtistId, Position) VALUES (@AlbumId, @ArtistId, @Position)";

	private readonly string _connectionString = options.Value.ConnectionString;

	public async Task<IReadOnlyList<Artist>> GetArtists(CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var artists = await connection.QueryAsync<ArtistDto>(SelectArtists);
		var externalIds = (await connection.QueryAsync<ArtistExternalIdDto>(SelectArtistExternalIds)).ToLookup();

		return artists
			.Select(a => a.ToArtist(externalIds))
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
		var albumExternalIds = (await connection.QueryAsync<AlbumExternalIdDto>(SelectAlbumExternalIds)).ToLookup();
		var artistExternalIds = (await connection.QueryAsync<ArtistExternalIdDto>(SelectArtistExternalIds)).ToLookup();

		return rows
			.GroupBy(r => r.Album.Id)
			.Select(g =>
				g.First().Album.ToAlbum(
					g.Where(r => r.AlbumArtist is not null)
					.Select(r => r.AlbumArtist!.ToAlbumArtist(artistExternalIds))
					.ToList(),
					albumExternalIds
				)
			)
			.ToList();
	}

	public async Task<IReadOnlyList<Album>> GetArtistAlbums(ArtistId artistId, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var parameters = new { ArtistId = artistId };
		var rows = await connection.QueryAsync<AlbumDto, AlbumArtistDto?, (AlbumDto Album, AlbumArtistDto? AlbumArtist)>(
			SelectArtistAlbums,
			(album, albumArtist) => (album, albumArtist),
			parameters,
			splitOn: "Position");
		var albumExternalIds = (await connection.QueryAsync<AlbumExternalIdDto>(SelectArtistAlbumExternalIds, parameters)).ToLookup();
		var artistExternalIds = (await connection.QueryAsync<ArtistExternalIdDto>(SelectArtistAlbumCreditExternalIds, parameters)).ToLookup();

		return rows
			.GroupBy(r => r.Album.Id)
			.Select(g =>
				g.First().Album.ToAlbum(
					g.Select(r => r.AlbumArtist!.ToAlbumArtist(artistExternalIds))
					.ToList(),
					albumExternalIds
				)
			)
			.ToList();
	}

	public async Task AddArtists(IReadOnlyList<Artist> artists, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var externalIdDtos = artists
			.SelectMany(artist => artist.ExternalIds.Select(externalId => externalId.ToDto(artist.Id)));

		await connection.ExecuteAsync(InsertArtist, artists.Select(a => a.ToDto()), transaction);
		await connection.ExecuteAsync(InsertArtistExternalId, externalIdDtos, transaction);

		await transaction.CommitAsync(ct);
	}

	public async Task AddAlbums(IReadOnlyList<Album> albums, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var albumDtos = albums.Select(album => album.ToDto());
		var externalIdDtos = albums
			.SelectMany(album => album.ExternalIds.Select(externalId => externalId.ToDto(album.Id)));
		var albumArtistDtos = albums
			.SelectMany(album => album.Artists.Select(albumArtist => albumArtist.ToDto(album.Id)));

		await connection.ExecuteAsync(InsertAlbum, albumDtos, transaction);
		await connection.ExecuteAsync(InsertAlbumExternalId, externalIdDtos, transaction);
		await connection.ExecuteAsync(InsertAlbumArtist, albumArtistDtos, transaction);

		await transaction.CommitAsync(ct);
	}
}
