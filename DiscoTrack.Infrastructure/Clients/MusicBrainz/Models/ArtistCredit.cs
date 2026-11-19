namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

public sealed record ArtistCredit
{
	public required string Name { get; init; }
	public required Artist Artist { get; init; }
}
