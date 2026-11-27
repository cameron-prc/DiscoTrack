namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

public sealed record Track
{
	public int Position { get; init; }
	public required string Title { get; init; }
	public int? Length { get; init; }
	public Recording? Recording { get; init; }
}
