using DiscoTrack.Playlists.ImportPlaylist;
using DiscoTrack.Playlists.SyncPlaylists;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs.JobHandlers;

public class SyncPlaylistsJobHandler(ISyncPlaylistsService syncPlaylists, IImportPlaylistService importPlaylist, ILogger<SyncPlaylistsJobHandler> logger) : ISyncPlaylistsJobHandler
{
	/// <summary>Refreshes what describes the tracked playlists, then brings each one up to date. One playlist failing does not stop the rest.</summary>
	public async Task Execute(CancellationToken cancellationToken)
	{
		var result = await syncPlaylists.Run(cancellationToken);

		if (result.IsLeft)
		{
			foreach (var error in result.GetLeft)
			{
				logger.LogError("Playlists sync failed: {Error}", error);
			}

			throw new JobFailedException($"Playlists sync failed with {result.GetLeft.Count} errors");
		}

		var response = result.GetRight;
		var trackedPlaylists = response.Playlists
			.Where(playlist => playlist.RemovedAt is null)
			.ToList();

		logger.LogInformation(
			"Playlists sync refreshed {Playlists} tracked playlists, {RemovedPlaylists} of them newly gone from the provider. Updating {TrackedPlaylists}",
			response.Playlists.Count,
			response.RemovedPlaylists.Count,
			trackedPlaylists.Count);

		var failedPlaylists = 0;

		foreach (var playlist in trackedPlaylists)
		{
			var import = await importPlaylist.Run(playlist.Id, onlyWhenChanged: true, cancellationToken);

			if (import.IsLeft)
			{
				failedPlaylists++;
				logger.LogError("Playlists sync failed to update {Playlist}: {Errors}", playlist, import.GetLeft);

				continue;
			}

			logger.LogInformation(
				"{Playlist} updated. Items copied: {CopiedItems}. {Items} items, {ResolvedItems} resolved, {UnresolvedItems} not resolved, {CreatedAlbums} albums created",
				playlist,
				import.GetRight.CopiedItems,
				import.GetRight.Items,
				import.GetRight.ResolvedItems,
				import.GetRight.UnresolvedItems,
				import.GetRight.CreatedAlbums.Count);
		}

		if (failedPlaylists > 0)
		{
			throw new JobFailedException($"Playlists sync could not update {failedPlaylists} of {trackedPlaylists.Count} tracked playlists");
		}
	}
}
