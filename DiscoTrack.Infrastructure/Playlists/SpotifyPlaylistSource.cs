using System.Text.RegularExpressions;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.Spotify;
using DiscoTrack.Infrastructure.Providers;
using DiscoTrack.Playlists;

namespace DiscoTrack.Infrastructure.Playlists;

public partial class SpotifyPlaylistSource(ISpotifyClient client) : IPlaylistSource
{
	// https://open.spotify.com/playlist/{id}?si=..., spotify:playlist:{id}, or the id alone
	[GeneratedRegex(@"^(?:https?://open\.spotify\.com/(?:[a-z-]+/)?playlist/|spotify:playlist:)?(?<id>[A-Za-z0-9]{22})(?:[?#/].*)?$")]
	private static partial Regex PlaylistReference();

	// The most Spotify returns per page
	private const int ItemPageSize = 100;
	private const string Song = "track";

	public Provider PlaylistProvider
	{
		get { return Provider.SpotifyPlaylist; }
	}


	public ExternalId? FindPlaylistId(string reference)
	{
		var match = PlaylistReference().Match(reference.Trim());

		return match.Success ? new SpotifyPlaylistId(match.Groups["id"].Value) : null;
	}

	public async Task<ProviderPlaylist?> GetPlaylist(ExternalId playlistId, CancellationToken ct)
	{
		if (playlistId is not SpotifyPlaylistId spotifyPlaylistId)
		{
			throw new ArgumentException($"{playlistId.Provider} is not a Spotify playlist id", nameof(playlistId));
		}

		var playlist = await client.GetPlaylist(spotifyPlaylistId.Value, ct);

		return playlist is null ? null : ToProviderPlaylist(playlist);
	}

	private static ProviderPlaylist ToProviderPlaylist(Clients.Spotify.Models.Playlist playlist)
	{
		return new ProviderPlaylist(
			new SpotifyPlaylistId(playlist.Id),
			playlist.Name,
			string.IsNullOrEmpty(playlist.Description) ? null : playlist.Description,
			playlist.Owner?.DisplayName ?? playlist.Owner?.Id,
			playlist.SnapshotId,
			playlist.Tracks?.Total ?? 0);
	}

	public async Task<IReadOnlyList<PlaylistItem>> GetItems(ExternalId playlistId, CancellationToken ct)
	{
		if (playlistId is not SpotifyPlaylistId spotifyPlaylistId)
		{
			throw new ArgumentException($"{playlistId.Provider} is not a Spotify playlist id", nameof(playlistId));
		}

		var songs = new List<Clients.Spotify.Models.PlaylistItem>();
		var offset = 0;

		while (true)
		{
			var page = await client.GetPlaylistItems(spotifyPlaylistId.Value, ItemPageSize, offset, ct);

			// A local file has no id, and an episode is not a song
			songs.AddRange(page.Items
				.OfType<Clients.Spotify.Models.PlaylistItem>()
				.Where(item => !item.IsLocal && item.Track is { Id: not null } track && (track.Type is null || track.Type == Song)));
			offset += page.Items.Count;

			if (page.Next is null || page.Items.Count == 0)
			{
				return songs
					.Select((item, index) => new PlaylistItem(index + 1, item.AddedAt, item.Track!.ToProviderTrack()))
					.ToList();
			}
		}
	}
}
