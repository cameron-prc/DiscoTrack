using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

namespace DiscoTrack.Infrastructure.Clients.MusicBrainz.Responses;

internal sealed class IsrcResponse
{
	public IReadOnlyList<Recording> Recordings { get; init; } = [];
}
