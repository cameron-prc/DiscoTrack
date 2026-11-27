using DiscoTrack.Playlists;
using DiscoTrack.Playlists.ImportPlaylist;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs.JobHandlers;

public class SyncPlaylistJobHandler(IImportPlaylistService importPlaylist, ILogger<SyncPlaylistJobHandler> logger) : ISyncPlaylistJobHandler
{
	public async Task Execute(PlaylistId playlistId, CancellationToken cancellationToken)
	{
		var result = await importPlaylist.Run(playlistId, onlyWhenChanged: false, cancellationToken);

		if (result.IsLeft)
		{
			foreach (var error in result.GetLeft)
			{
				logger.LogError("Playlist {PlaylistId} sync failed: {Error}", playlistId, error);
			}

			throw new JobFailedException($"Playlist {playlistId} sync failed with {result.GetLeft.Count} errors");
		}

		var response = result.GetRight;

		logger.LogInformation(
			"Playlist {Playlist} sync completed. {Items} items, {ResolvedItems} resolved to catalogue tracks, {UnresolvedItems} not resolved, {CreatedAlbums} albums created",
			response.Playlist,
			response.Items,
			response.ResolvedItems,
			response.UnresolvedItems,
			response.CreatedAlbums.Count);
	}
}
