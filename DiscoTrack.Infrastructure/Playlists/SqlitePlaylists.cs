using System.Globalization;
using Dapper;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.Playlists;
using DiscoTrack.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using static DiscoTrack.Infrastructure.Catalogue.Extensions;

namespace DiscoTrack.Infrastructure.Playlists;

public class SqlitePlaylists(IOptions<SqliteOptions> options) : IPlaylists
{
	private const string PlaylistColumns = "Id, Provider, Value, Name, Description, OwnerName, SnapshotId, ItemCount, Tracked, ImportedSnapshotId, LastImportedAt, RemovedAt";

	private const string SelectPlaylists = $"SELECT {PlaylistColumns} FROM Playlists ORDER BY Name COLLATE NOCASE, Id";

	private const string SelectPlaylist = $"SELECT {PlaylistColumns} FROM Playlists WHERE Id = @Id";

	private const string UpsertPlaylist = $"""
		INSERT INTO Playlists ({PlaylistColumns})
		VALUES (@Id, @Provider, @Value, @Name, @Description, @OwnerName, @SnapshotId, @ItemCount, @Tracked, @ImportedSnapshotId, @LastImportedAt, @RemovedAt)
		ON CONFLICT (Id) DO UPDATE SET
			Name = excluded.Name,
			Description = excluded.Description,
			OwnerName = excluded.OwnerName,
			SnapshotId = excluded.SnapshotId,
			ItemCount = excluded.ItemCount,
			Tracked = excluded.Tracked,
			ImportedSnapshotId = excluded.ImportedSnapshotId,
			LastImportedAt = excluded.LastImportedAt,
			RemovedAt = excluded.RemovedAt
		""";

	private const string ItemColumns = "PlaylistId, Position, AddedAt, TrackProvider, TrackValue, TrackTitle, Isrc, DurationMs, DiscNumber, TrackNumber, AlbumProvider, AlbumValue, AlbumTitle";

	private const string SelectItems = $"SELECT {ItemColumns} FROM PlaylistItems WHERE PlaylistId = @PlaylistId ORDER BY Position";

	private const string SelectItemArtists = "SELECT PlaylistId, ItemPosition, Position, Provider, Value, Name FROM PlaylistItemArtists WHERE PlaylistId = @PlaylistId ORDER BY ItemPosition, Position";

	private const string InsertItem = $"""
		INSERT INTO PlaylistItems ({ItemColumns})
		VALUES (@PlaylistId, @Position, @AddedAt, @TrackProvider, @TrackValue, @TrackTitle, @Isrc, @DurationMs, @DiscNumber, @TrackNumber, @AlbumProvider, @AlbumValue, @AlbumTitle)
		""";

	private const string InsertItemArtist = "INSERT INTO PlaylistItemArtists (PlaylistId, ItemPosition, Position, Provider, Value, Name) VALUES (@PlaylistId, @ItemPosition, @Position, @Provider, @Value, @Name)";

	private const string DeleteItemArtists = "DELETE FROM PlaylistItemArtists WHERE PlaylistId = @PlaylistId";

	private const string DeleteItems = "DELETE FROM PlaylistItems WHERE PlaylistId = @PlaylistId";

	private readonly string _connectionString = options.Value.ConnectionString;

	public async Task<IReadOnlyList<Playlist>> GetPlaylists(CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		return (await connection.QueryAsync<PlaylistRow>(SelectPlaylists)).Select(ToPlaylist).ToList();
	}

	public async Task<Playlist?> GetPlaylist(PlaylistId playlistId, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var row = await connection.QuerySingleOrDefaultAsync<PlaylistRow>(SelectPlaylist, new { Id = playlistId.ToString() });

		return row is null ? null : ToPlaylist(row);
	}

	public async Task SavePlaylist(Playlist playlist, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		await connection.ExecuteAsync(UpsertPlaylist, ToRow(playlist));
	}

	public async Task<IReadOnlyList<PlaylistItem>> GetItems(PlaylistId playlistId, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var parameters = new { PlaylistId = playlistId.ToString() };
		var items = await connection.QueryAsync<ItemRow>(SelectItems, parameters);
		var artists = (await connection.QueryAsync<ItemArtistRow>(SelectItemArtists, parameters)).ToLookup(artist => artist.ItemPosition);

		return items
			.Select(item => new PlaylistItem(
				item.Position,
				item.AddedAt is null ? null : ParseInstant(item.AddedAt),
				new ProviderTrack
				{
					Id = ToExternalId(item.TrackProvider, item.TrackValue),
					Title = item.TrackTitle,
					Isrc = item.Isrc,
					DurationMs = item.DurationMs,
					DiscNumber = item.DiscNumber,
					TrackNumber = item.TrackNumber,
					Artists = artists[item.Position].Select(artist => new ProviderArtist(ToExternalId(artist.Provider, artist.Value), artist.Name)).ToList(),
					AlbumId = item.AlbumProvider is null || item.AlbumValue is null ? null : ToExternalId(item.AlbumProvider, item.AlbumValue),
					AlbumTitle = item.AlbumTitle,
				}))
			.ToList();
	}

	public async Task ReplaceItems(PlaylistId playlistId, IReadOnlyList<PlaylistItem> items, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var id = playlistId.ToString();

		await connection.ExecuteAsync(DeleteItemArtists, new { PlaylistId = id }, transaction);
		await connection.ExecuteAsync(DeleteItems, new { PlaylistId = id }, transaction);
		await connection.ExecuteAsync(InsertItem, items.Select(item => ToRow(id, item)), transaction);
		await connection.ExecuteAsync(
			InsertItemArtist,
			items.SelectMany(item => item.Track.Artists.Select((artist, index) => new ItemArtistRow
			{
				PlaylistId = id,
				ItemPosition = item.Position,
				Position = index + 1,
				Provider = artist.Id.Provider.ToString(),
				Value = artist.Id.Value,
				Name = artist.Name,
			})),
			transaction);

		await transaction.CommitAsync(ct);
	}

	private static string FormatInstant(DateTimeOffset instant)
	{
		return instant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
	}

	private static DateTimeOffset ParseInstant(string instant)
	{
		return DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
	}

	private static Playlist ToPlaylist(PlaylistRow row)
	{
		return new Playlist
		{
			Id = new PlaylistId(Guid.Parse(row.Id)),
			ExternalId = ToExternalId(row.Provider, row.Value),
			Name = row.Name,
			Description = row.Description,
			OwnerName = row.OwnerName,
			SnapshotId = row.SnapshotId,
			ItemCount = row.ItemCount,
			Tracked = row.Tracked,
			ImportedSnapshotId = row.ImportedSnapshotId,
			LastImportedAt = row.LastImportedAt is null ? null : ParseInstant(row.LastImportedAt),
			RemovedAt = row.RemovedAt is null ? null : ParseInstant(row.RemovedAt),
		};
	}

	private static PlaylistRow ToRow(Playlist playlist)
	{
		return new PlaylistRow
		{
			Id = playlist.Id.ToString(),
			Provider = playlist.ExternalId.Provider.ToString(),
			Value = playlist.ExternalId.Value,
			Name = playlist.Name,
			Description = playlist.Description,
			OwnerName = playlist.OwnerName,
			SnapshotId = playlist.SnapshotId,
			ItemCount = playlist.ItemCount,
			Tracked = playlist.Tracked,
			ImportedSnapshotId = playlist.ImportedSnapshotId,
			LastImportedAt = playlist.LastImportedAt is { } importedAt ? FormatInstant(importedAt) : null,
			RemovedAt = playlist.RemovedAt is { } removedAt ? FormatInstant(removedAt) : null,
		};
	}

	private static ItemRow ToRow(string playlistId, PlaylistItem item)
	{
		return new ItemRow
		{
			PlaylistId = playlistId,
			Position = item.Position,
			AddedAt = item.AddedAt is { } addedAt ? FormatInstant(addedAt) : null,
			TrackProvider = item.Track.Id.Provider.ToString(),
			TrackValue = item.Track.Id.Value,
			TrackTitle = item.Track.Title,
			Isrc = item.Track.Isrc,
			DurationMs = item.Track.DurationMs,
			DiscNumber = item.Track.DiscNumber,
			TrackNumber = item.Track.TrackNumber,
			AlbumProvider = item.Track.AlbumId?.Provider.ToString(),
			AlbumValue = item.Track.AlbumId?.Value,
			AlbumTitle = item.Track.AlbumTitle,
		};
	}

	private sealed class PlaylistRow
	{
		public string Id { get; init; } = "";
		public string Provider { get; init; } = "";
		public string Value { get; init; } = "";
		public string Name { get; init; } = "";
		public string? Description { get; init; }
		public string? OwnerName { get; init; }
		public string? SnapshotId { get; init; }
		public int ItemCount { get; init; }
		public bool Tracked { get; init; }
		public string? ImportedSnapshotId { get; init; }
		public string? LastImportedAt { get; init; }
		public string? RemovedAt { get; init; }
	}

	private sealed class ItemRow
	{
		public string PlaylistId { get; init; } = "";
		public int Position { get; init; }
		public string? AddedAt { get; init; }
		public string TrackProvider { get; init; } = "";
		public string TrackValue { get; init; } = "";
		public string TrackTitle { get; init; } = "";
		public string? Isrc { get; init; }
		public int? DurationMs { get; init; }
		public int? DiscNumber { get; init; }
		public int? TrackNumber { get; init; }
		public string? AlbumProvider { get; init; }
		public string? AlbumValue { get; init; }
		public string? AlbumTitle { get; init; }
	}

	private sealed class ItemArtistRow
	{
		public string PlaylistId { get; init; } = "";
		public int ItemPosition { get; init; }
		public int Position { get; init; }
		public string Provider { get; init; } = "";
		public string Value { get; init; } = "";
		public string Name { get; init; } = "";
	}
}
