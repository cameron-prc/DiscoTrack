namespace DiscoTrack.Core.Artists;

public class Artist
{
	public ArtistId Id { get; init; } = ArtistId.New();
	public string? SubsonicId { get; set; }
	public required string Name { get; set; }
}
