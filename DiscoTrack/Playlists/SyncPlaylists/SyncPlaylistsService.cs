using DiscoTrack.Common;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Playlists.SyncPlaylists;

public class SyncPlaylistsService(IPlaylistSource source, IPlaylists playlists, TimeProvider timeProvider, ILogger<SyncPlaylistsService> logger) : ISyncPlaylistsService
{
	/// <summary>
	/// <para>Refreshes what describes each tracked playlist of the source, not its items. Playlists are only ever added by a person; none is added here.</para>
	/// <para>A playlist the source no longer has is marked removed and kept; it is unmarked if the source has it again.</para>
	/// </summary>
	public async Task<Either<IList<ISyncPlaylistsError>, SyncPlaylistsResponse>> Run(CancellationToken cancellationToken)
	{
		IReadOnlyList<Playlist> trackedPlaylists;
		var changedPlaylists = new List<Playlist>();
		var removedPlaylists = new List<Playlist>();

		try
		{
			trackedPlaylists = (await playlists.GetPlaylists(cancellationToken))
				.Where(playlist => playlist.Tracked && playlist.ExternalId.Provider == source.PlaylistProvider)
				.ToList();
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncPlaylistsError> { new UnableToReadPlaylistsError(exception.Message) };
		}

		try
		{
			foreach (var playlist in trackedPlaylists)
			{
				var providerPlaylist = await source.GetPlaylist(playlist.ExternalId, cancellationToken);

				if (providerPlaylist is null)
				{
					if (playlist.RemovedAt is null)
					{
						playlist.RemovedAt = timeProvider.GetUtcNow();
						removedPlaylists.Add(playlist);
					}
				}
				else if (Differs(playlist, providerPlaylist))
				{
					Describe(playlist, providerPlaylist);
					changedPlaylists.Add(playlist);
				}
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<ISyncPlaylistsError> { new UnableToReadPlaylistSourceError(exception.Message) };
		}

		try
		{
			foreach (var playlist in changedPlaylists.Concat(removedPlaylists))
			{
				await playlists.SavePlaylist(playlist, cancellationToken);
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save the refreshed playlists");

			return new List<ISyncPlaylistsError> { new UnableToSavePlaylistError(exception.Message) };
		}

		return new SyncPlaylistsResponse(trackedPlaylists, removedPlaylists);
	}

	private static bool Differs(Playlist playlist, ProviderPlaylist providerPlaylist)
	{
		return playlist.Name != providerPlaylist.Name
			|| playlist.Description != providerPlaylist.Description
			|| playlist.OwnerName != providerPlaylist.OwnerName
			|| playlist.SnapshotId != providerPlaylist.SnapshotId
			|| playlist.ItemCount != providerPlaylist.ItemCount
			|| playlist.RemovedAt is not null;
	}

	private static void Describe(Playlist playlist, ProviderPlaylist providerPlaylist)
	{
		playlist.Name = providerPlaylist.Name;
		playlist.Description = providerPlaylist.Description;
		playlist.OwnerName = providerPlaylist.OwnerName;
		playlist.SnapshotId = providerPlaylist.SnapshotId;
		playlist.ItemCount = providerPlaylist.ItemCount;
		playlist.RemovedAt = null;
	}
}
