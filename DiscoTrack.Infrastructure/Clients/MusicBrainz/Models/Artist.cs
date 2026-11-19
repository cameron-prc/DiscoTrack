namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

public sealed record Artist
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public string? Disambiguation { get; init; }
}
