using DiscoTrack.ExternalIds;

namespace DiscoTrack.Playlists;

public interface IPlaylistSource
{
	Provider PlaylistProvider { get; }

	/// <summary>Reads a playlist id out of what a person would paste: a link to the playlist, or the id itself. Null when it is neither.</summary>
	ExternalId? FindPlaylistId(string reference);

	/// <summary>The playlist, whoever owns it. Null when the provider has no such playlist.</summary>
	Task<ProviderPlaylist?> GetPlaylist(ExternalId playlistId, CancellationToken ct);

	/// <summary>The playlist's songs in order. Anything that is not a song is left out.</summary>
	Task<IReadOnlyList<PlaylistItem>> GetItems(ExternalId playlistId, CancellationToken ct);
}
