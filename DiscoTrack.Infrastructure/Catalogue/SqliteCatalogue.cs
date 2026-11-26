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
		SELECT al.Id, al.Title, al.Year, al.IsCompilation, al.Tracked,
		       aa.Position, ar.Id AS ArtistId, ar.Name
		FROM Albums al
		LEFT JOIN AlbumArtists aa ON aa.AlbumId = al.Id
		LEFT JOIN Artists ar ON ar.Id = aa.ArtistId
		ORDER BY al.Title, aa.Position
		""";

	private const string SelectAlbumExternalIds = "SELECT AlbumId, Provider, Value FROM AlbumExternalIds";

	private const string SelectArtistAlbums = """
		SELECT al.Id, al.Title, al.Year, al.IsCompilation, al.Tracked,
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

	private const string SelectArtist = "SELECT Id, Name FROM Artists WHERE Id = @ArtistId";

	private const string SelectArtistExternalIdsByArtist = "SELECT ArtistId, Provider, Value FROM ArtistExternalIds WHERE ArtistId = @ArtistId";

	private const string SelectAlbum = """
		SELECT al.Id, al.Title, al.Year, al.IsCompilation, al.Tracked,
		       aa.Position, ar.Id AS ArtistId, ar.Name
		FROM Albums al
		LEFT JOIN AlbumArtists aa ON aa.AlbumId = al.Id
		LEFT JOIN Artists ar ON ar.Id = aa.ArtistId
		WHERE al.Id = @AlbumId
		ORDER BY aa.Position
		""";

	private const string SelectAlbumExternalIdsByAlbum = "SELECT AlbumId, Provider, Value FROM AlbumExternalIds WHERE AlbumId = @AlbumId";

	private const string SelectAlbumArtists = """
		SELECT ar.Id, ar.Name
		FROM Artists ar
		JOIN AlbumArtists aa ON aa.ArtistId = ar.Id
		WHERE aa.AlbumId = @AlbumId
		ORDER BY aa.Position
		""";

	private const string SelectAlbumCreditExternalIds = """
		SELECT ArtistId, Provider, Value
		FROM ArtistExternalIds
		WHERE ArtistId IN (SELECT ArtistId FROM AlbumArtists WHERE AlbumId = @AlbumId)
		""";

	private const string UpsertArtist = """
		INSERT INTO Artists (Id, Name) VALUES (@Id, @Name)
		ON CONFLICT (Id) DO UPDATE SET Name = excluded.Name
		""";

	private const string DeleteArtistExternalIds = "DELETE FROM ArtistExternalIds WHERE ArtistId = @Id";

	private const string InsertArtistExternalId = "INSERT INTO ArtistExternalIds (ArtistId, Provider, Value) VALUES (@ArtistId, @Provider, @Value)";

	private const string UpsertAlbum = """
		INSERT INTO Albums (Id, Title, Year, IsCompilation, Tracked) VALUES (@Id, @Title, @Year, @IsCompilation, @Tracked)
		ON CONFLICT (Id) DO UPDATE SET Title = excluded.Title, Year = excluded.Year, IsCompilation = excluded.IsCompilation, Tracked = excluded.Tracked
		""";

	private const string DeleteAlbumExternalIds = "DELETE FROM AlbumExternalIds WHERE AlbumId = @Id";

	private const string InsertAlbumExternalId = "INSERT INTO AlbumExternalIds (AlbumId, Provider, Value) VALUES (@AlbumId, @Provider, @Value)";

	private const string DeleteAlbumArtists = "DELETE FROM AlbumArtists WHERE AlbumId = @Id";

	private const string InsertAlbumArtist = "INSERT INTO AlbumArtists (AlbumId, ArtistId, Position) VALUES (@AlbumId, @ArtistId, @Position)";

	private const string MoveAlbumArtists = """
		UPDATE AlbumArtists SET ArtistId = @Survivor
		WHERE ArtistId = @Absorbed
		  AND AlbumId NOT IN (SELECT AlbumId FROM AlbumArtists WHERE ArtistId = @Survivor)
		""";

	private const string DeleteArtistAlbumArtists = "DELETE FROM AlbumArtists WHERE ArtistId = @Absorbed";

	private const string DeleteArtist = "DELETE FROM Artists WHERE Id = @Absorbed";

	private const string DeleteAlbum = "DELETE FROM Albums WHERE Id = @Absorbed";

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

	public async Task<Artist?> GetArtist(ArtistId artistId, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var parameters = new { ArtistId = artistId };
		var artist = await connection.QuerySingleOrDefaultAsync<ArtistDto>(SelectArtist, parameters);
		var externalIds = (await connection.QueryAsync<ArtistExternalIdDto>(SelectArtistExternalIdsByArtist, parameters)).ToLookup();

		return artist?.ToArtist(externalIds);
	}

	public async Task<Album?> GetAlbum(AlbumId albumId, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var parameters = new { AlbumId = albumId };
		var rows = (await connection.QueryAsync<AlbumDto, AlbumArtistDto?, (AlbumDto Album, AlbumArtistDto? AlbumArtist)>(
			SelectAlbum,
			(album, albumArtist) => (album, albumArtist),
			parameters,
			splitOn: "Position")).ToList();
		var albumExternalIds = (await connection.QueryAsync<AlbumExternalIdDto>(SelectAlbumExternalIdsByAlbum, parameters)).ToLookup();
		var artistExternalIds = (await connection.QueryAsync<ArtistExternalIdDto>(SelectAlbumCreditExternalIds, parameters)).ToLookup();

		return rows.FirstOrDefault().Album?.ToAlbum(
			rows.Where(r => r.AlbumArtist is not null)
				.Select(r => r.AlbumArtist!.ToAlbumArtist(artistExternalIds))
				.ToList(),
			albumExternalIds
		);
	}

	public async Task<IReadOnlyList<Artist>> GetAlbumArtists(AlbumId albumId, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var parameters = new { AlbumId = albumId };
		var artists = await connection.QueryAsync<ArtistDto>(SelectAlbumArtists, parameters);
		var externalIds = (await connection.QueryAsync<ArtistExternalIdDto>(SelectAlbumCreditExternalIds, parameters)).ToLookup();

		return artists
			.Select(a => a.ToArtist(externalIds))
			.ToList();
	}

	public async Task SaveArtist(Artist artist, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var artistDto = artist.ToDto();
		var externalIdDtos = artist.ExternalIds.Select(externalId => externalId.ToDto(artist.Id));

		await connection.ExecuteAsync(UpsertArtist, artistDto, transaction);
		await connection.ExecuteAsync(DeleteArtistExternalIds, artistDto, transaction);
		await connection.ExecuteAsync(InsertArtistExternalId, externalIdDtos, transaction);

		await transaction.CommitAsync(ct);
	}

	public async Task SaveAlbum(Album album, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var albumDto = album.ToDto();
		var externalIdDtos = album.ExternalIds.Select(externalId => externalId.ToDto(album.Id));
		var albumArtistDtos = album.Artists.Select(albumArtist => albumArtist.ToDto(album.Id));

		await connection.ExecuteAsync(UpsertAlbum, albumDto, transaction);
		await connection.ExecuteAsync(DeleteAlbumExternalIds, albumDto, transaction);
		await connection.ExecuteAsync(DeleteAlbumArtists, albumDto, transaction);
		await connection.ExecuteAsync(InsertAlbumExternalId, externalIdDtos, transaction);
		await connection.ExecuteAsync(InsertAlbumArtist, albumArtistDtos, transaction);

		await transaction.CommitAsync(ct);
	}

	public async Task MergeArtists(Artist survivor, ArtistId absorbed, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var artistDto = survivor.ToDto();
		var externalIdDtos = survivor.ExternalIds.Select(externalId => externalId.ToDto(survivor.Id));
		var parameters = new { Survivor = survivor.Id, Absorbed = absorbed };

		await connection.ExecuteAsync(UpsertArtist, artistDto, transaction);
		await connection.ExecuteAsync(MoveAlbumArtists, parameters, transaction);
		await connection.ExecuteAsync(DeleteArtistAlbumArtists, parameters, transaction);
		await connection.ExecuteAsync(DeleteArtistExternalIds, new { Id = absorbed }, transaction);
		await connection.ExecuteAsync(DeleteArtist, parameters, transaction);
		await connection.ExecuteAsync(DeleteArtistExternalIds, artistDto, transaction);
		await connection.ExecuteAsync(InsertArtistExternalId, externalIdDtos, transaction);

		await transaction.CommitAsync(ct);
	}

	public async Task MergeAlbums(Album survivor, AlbumId absorbed, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var albumDto = survivor.ToDto();
		var externalIdDtos = survivor.ExternalIds.Select(externalId => externalId.ToDto(survivor.Id));
		var albumArtistDtos = survivor.Artists.Select(albumArtist => albumArtist.ToDto(survivor.Id));
		var parameters = new { Absorbed = absorbed };

		await connection.ExecuteAsync(DeleteAlbumExternalIds, new { Id = absorbed }, transaction);
		await connection.ExecuteAsync(DeleteAlbumArtists, new { Id = absorbed }, transaction);
		await connection.ExecuteAsync(DeleteAlbum, parameters, transaction);
		await connection.ExecuteAsync(UpsertAlbum, albumDto, transaction);
		await connection.ExecuteAsync(DeleteAlbumExternalIds, albumDto, transaction);
		await connection.ExecuteAsync(DeleteAlbumArtists, albumDto, transaction);
		await connection.ExecuteAsync(InsertAlbumExternalId, externalIdDtos, transaction);
		await connection.ExecuteAsync(InsertAlbumArtist, albumArtistDtos, transaction);

		await transaction.CommitAsync(ct);
	}
}
