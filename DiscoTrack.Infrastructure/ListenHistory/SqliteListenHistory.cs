using System.Globalization;
using Dapper;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Sqlite;
using DiscoTrack.ListenHistory;
using DiscoTrack.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using static DiscoTrack.Infrastructure.Catalogue.Extensions;

namespace DiscoTrack.Infrastructure.ListenHistory;

public class SqliteListenHistory(IOptions<SqliteOptions> options) : IListenHistory
{
	private const string PlayedAtFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

	private const string SelectLatestPlayedAt = "SELECT MAX(PlayedAt) FROM Plays WHERE TrackProvider = @TrackProvider";

	private const string InsertPlay = """
		INSERT OR IGNORE INTO Plays (Id, PlayedAt, TrackProvider, TrackValue, TrackTitle, Isrc, DurationMs, DiscNumber, TrackNumber, AlbumProvider, AlbumValue, AlbumTitle)
		VALUES (@Id, @PlayedAt, @TrackProvider, @TrackValue, @TrackTitle, @Isrc, @DurationMs, @DiscNumber, @TrackNumber, @AlbumProvider, @AlbumValue, @AlbumTitle)
		""";

	private const string InsertPlayArtist = "INSERT INTO PlayArtists (PlayId, Position, Provider, Value, Name) VALUES (@PlayId, @Position, @Provider, @Value, @Name)";

	private const string SelectPlays = """
		SELECT Id, PlayedAt, TrackProvider, TrackValue, TrackTitle, Isrc, DurationMs, DiscNumber, TrackNumber, AlbumProvider, AlbumValue, AlbumTitle
		FROM Plays
		ORDER BY PlayedAt DESC, Id
		LIMIT @Limit OFFSET @Offset
		""";

	private const string SelectPlayArtists = "SELECT PlayId, Position, Provider, Value, Name FROM PlayArtists WHERE PlayId IN @PlayIds ORDER BY PlayId, Position";

	private const string CountAllPlays = "SELECT COUNT(*) FROM Plays";

	private readonly string _connectionString = options.Value.ConnectionString;

	public async Task<DateTimeOffset?> GetLatestPlayedAt(Provider trackProvider, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var latest = await connection.ExecuteScalarAsync<string?>(SelectLatestPlayedAt, new { TrackProvider = trackProvider.ToString() });

		return latest is null ? null : ParsePlayedAt(latest);
	}

	public async Task<IReadOnlyList<Play>> AddPlays(IReadOnlyList<Play> plays, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);

		var added = new List<Play>();

		foreach (var play in plays)
		{
			var inserted = await connection.ExecuteAsync(InsertPlay, ToRow(play), transaction);
			if (inserted == 0)
			{
				continue;
			}

			await connection.ExecuteAsync(InsertPlayArtist, play.Track.Artists.Select((artist, index) => ToRow(play.Id, index + 1, artist)), transaction);
			added.Add(play);
		}

		await transaction.CommitAsync(ct);

		return added;
	}

	public async Task<IReadOnlyList<Play>> GetPlays(int offset, int limit, CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		var plays = (await connection.QueryAsync<PlayRow>(SelectPlays, new { Offset = offset, Limit = limit })).ToList();
		var artists = (await connection.QueryAsync<PlayArtistRow>(SelectPlayArtists, new { PlayIds = plays.Select(play => play.Id) }))
			.ToLookup(artist => artist.PlayId);

		return plays
			.Select(play => new Play
			{
				Id = new PlayId(Guid.Parse(play.Id)),
				PlayedAt = ParsePlayedAt(play.PlayedAt),
				Track = new ProviderTrack
				{
					Id = ToExternalId(play.TrackProvider, play.TrackValue),
					Title = play.TrackTitle,
					Isrc = play.Isrc,
					DurationMs = play.DurationMs,
					DiscNumber = play.DiscNumber,
					TrackNumber = play.TrackNumber,
					Artists = artists[play.Id].Select(artist => new ProviderArtist(ToExternalId(artist.Provider, artist.Value), artist.Name)).ToList(),
					AlbumId = play.AlbumProvider is null || play.AlbumValue is null ? null : ToExternalId(play.AlbumProvider, play.AlbumValue),
					AlbumTitle = play.AlbumTitle,
				},
			})
			.ToList();
	}

	public async Task<int> CountPlays(CancellationToken ct)
	{
		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(ct);

		return await connection.ExecuteScalarAsync<int>(CountAllPlays);
	}

	private static DateTimeOffset ParsePlayedAt(string playedAt)
	{
		return DateTimeOffset.ParseExact(playedAt, PlayedAtFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
	}

	private static PlayRow ToRow(Play play)
	{
		return new PlayRow
		{
			Id = play.Id.ToString(),
			PlayedAt = play.PlayedAt.ToUniversalTime().ToString(PlayedAtFormat, CultureInfo.InvariantCulture),
			TrackProvider = play.Track.Id.Provider.ToString(),
			TrackValue = play.Track.Id.Value,
			TrackTitle = play.Track.Title,
			Isrc = play.Track.Isrc,
			DurationMs = play.Track.DurationMs,
			DiscNumber = play.Track.DiscNumber,
			TrackNumber = play.Track.TrackNumber,
			AlbumProvider = play.Track.AlbumId?.Provider.ToString(),
			AlbumValue = play.Track.AlbumId?.Value,
			AlbumTitle = play.Track.AlbumTitle,
		};
	}

	private static PlayArtistRow ToRow(PlayId playId, int position, ProviderArtist artist)
	{
		return new PlayArtistRow
		{
			PlayId = playId.ToString(),
			Position = position,
			Provider = artist.Id.Provider.ToString(),
			Value = artist.Id.Value,
			Name = artist.Name,
		};
	}

	private sealed class PlayRow
	{
		public string Id { get; init; } = "";
		public string PlayedAt { get; init; } = "";
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

	private sealed class PlayArtistRow
	{
		public string PlayId { get; init; } = "";
		public int Position { get; init; }
		public string Provider { get; init; } = "";
		public string Value { get; init; } = "";
		public string Name { get; init; } = "";
	}
}
