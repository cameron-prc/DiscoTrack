using DiscoTrack.ExternalIds;

namespace DiscoTrack.Providers;

/// <summary>A track as a provider describes it, wherever the provider mentions it: in a play, in a playlist.</summary>
public record ProviderTrack
{
	public required ExternalId Id { get; init; }
	public required string Title { get; init; }
	public string? Isrc { get; init; }
	public int? DurationMs { get; init; }
	public int? DiscNumber { get; init; }
	public int? TrackNumber { get; init; }
	public IReadOnlyList<ProviderArtist> Artists { get; init; } = [];
	public ExternalId? AlbumId { get; init; }
	public string? AlbumTitle { get; init; }

	public sealed override string ToString()
	{
		return $"{Title} ({Id})";
	}
}
