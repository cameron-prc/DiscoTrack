using System.Text.Json.Serialization;

namespace DiscoTrack.Infrastructure.Clients.Subsonic.Responses;

internal sealed class SubsonicEnvelope<T> where T : SubsonicResponse
{
	[JsonPropertyName("subsonic-response")]
	public required T Response { get; init; }
}
