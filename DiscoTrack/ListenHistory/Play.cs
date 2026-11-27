using DiscoTrack.ExternalIds;

namespace DiscoTrack.ListenHistory;

/// <summary>One listen, as the provider reported it. A track played at an instant is one play however often it is copied.</summary>
public record Play
{
	public PlayId Id { get; init; } = PlayId.New();
	public required DateTimeOffset PlayedAt { get; init; }
	public required ExternalId TrackId { get; init; }
	public required string TrackTitle { get; init; }
	public string? Isrc { get; init; }
	public int? DurationMs { get; init; }
	public int? DiscNumber { get; init; }
	public int? TrackNumber { get; init; }
	public IReadOnlyList<PlayArtist> Artists { get; init; } = [];
	public ExternalId? AlbumId { get; init; }
	public string? AlbumTitle { get; init; }

	public override string ToString()
	{
		return $"{TrackTitle} at {PlayedAt:O} ({Id})";
	}
}
