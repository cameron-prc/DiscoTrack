using DiscoTrack.Common;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Playlists.AddPlaylist;

public class AddPlaylistService(IPlaylistSource source, IPlaylists playlists, ILogger<AddPlaylistService> logger) : IAddPlaylistService
{
	/// <summary>
	/// <para>Tracks the playlist a pasted link or id refers to, adding it when it is not held yet. Its items are not copied here.</para>
	/// <para>The playlist need not be one of the account's own: any playlist the source can read by its id can be added.</para>
	/// </summary>
	public async Task<Either<IList<IAddPlaylistError>, AddPlaylistResponse>> Run(string reference, CancellationToken cancellationToken)
	{
		ProviderPlaylist? providerPlaylist;
		Playlist? knownPlaylist;

		var playlistId = source.FindPlaylistId(reference);

		if (playlistId is null)
		{
			return new List<IAddPlaylistError> { new UnrecognisedPlaylistReferenceError() };
		}

		try
		{
			providerPlaylist = await source.GetPlaylist(playlistId, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<IAddPlaylistError> { new UnableToReadPlaylistSourceError(exception.Message) };
		}

		if (providerPlaylist is null)
		{
			return new List<IAddPlaylistError> { new UnableToFindPlaylistAtSourceError() };
		}

		try
		{
			knownPlaylist = (await playlists.GetPlaylists(cancellationToken)).FirstOrDefault(playlist => playlist.ExternalId == providerPlaylist.Id);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<IAddPlaylistError> { new UnableToReadPlaylistsError(exception.Message) };
		}

		var alreadyTracked = knownPlaylist is { Tracked: true };
		var playlist = knownPlaylist ?? new Playlist { ExternalId = providerPlaylist.Id, Name = providerPlaylist.Name };

		playlist.Name = providerPlaylist.Name;
		playlist.Description = providerPlaylist.Description;
		playlist.OwnerName = providerPlaylist.OwnerName;
		playlist.SnapshotId = providerPlaylist.SnapshotId;
		playlist.ItemCount = providerPlaylist.ItemCount;
		playlist.RemovedAt = null;
		playlist.Tracked = true;

		try
		{
			await playlists.SavePlaylist(playlist, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to save {Playlist}", playlist);

			return new List<IAddPlaylistError> { new UnableToSavePlaylistError(exception.Message) };
		}

		return new AddPlaylistResponse(playlist, alreadyTracked);
	}
}
