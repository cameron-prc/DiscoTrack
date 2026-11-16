using DiscoTrack.Core.ExternalIds;

namespace DiscoTrack.Core.Artists;

public class Artist
{
	public ArtistId Id { get; init; } = ArtistId.New();
	public IReadOnlyList<ExternalId> ExternalIds { get; set; } = [];
	public required string Name { get; set; }
}
