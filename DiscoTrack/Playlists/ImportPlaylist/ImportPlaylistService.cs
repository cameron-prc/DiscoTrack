using DiscoTrack.Common;
using DiscoTrack.Providers.ResolveProviderTracks;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Playlists.ImportPlaylist;

public class ImportPlaylistService(
	IPlaylistSource source,
	IPlaylists playlists,
	IResolveProviderTracksService resolveProviderTracks,
	TimeProvider timeProvider,
	ILogger<ImportPlaylistService> logger) : IImportPlaylistService
{
	/// <summary>
	/// <para>Copies the playlist's items from the source, replacing the ones held, then makes the catalogue hold every item's track.</para>
	/// <para>With onlyWhenChanged the copy is skipped when the playlist still carries the marker its items were copied at.</para>
	/// <para>The items are resolved on every run, copied or not, so a run that was cut short is finished by the next one.</para>
	/// </summary>
	public async Task<Either<IList<IImportPlaylistError>, ImportPlaylistResponse>> Run(PlaylistId playlistId, bool onlyWhenChanged, CancellationToken cancellationToken)
	{
		Playlist? playlist;
		IReadOnlyList<PlaylistItem> items;

		try
		{
			playlist = await playlists.GetPlaylist(playlistId, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<IImportPlaylistError> { new UnableToReadPlaylistError(exception.Message) };
		}

		if (playlist is null)
		{
			return new List<IImportPlaylistError> { new UnableToFindPlaylistError() };
		}

		var copyItems = !onlyWhenChanged || playlist.HasChangedSinceImport;

		if (copyItems)
		{
			try
			{
				items = await source.GetItems(playlist.ExternalId, cancellationToken);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				return new List<IImportPlaylistError> { new UnableToReadPlaylistItemsFromSourceError(exception.Message) };
			}

			playlist.ImportedSnapshotId = playlist.SnapshotId;
			playlist.LastImportedAt = timeProvider.GetUtcNow();

			try
			{
				await playlists.ReplaceItems(playlist.Id, items, cancellationToken);
				await playlists.SavePlaylist(playlist, cancellationToken);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				logger.LogWarning(exception, "Failed to save the {ItemCount} items of {Playlist}", items.Count, playlist);

				return new List<IImportPlaylistError> { new UnableToSavePlaylistItemsError(exception.Message) };
			}
		}
		else
		{
			try
			{
				items = await playlists.GetItems(playlist.Id, cancellationToken);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				return new List<IImportPlaylistError> { new UnableToReadPlaylistError(exception.Message) };
			}
		}

		var resolution = await resolveProviderTracks.Run(items.Select(item => item.Track).ToList(), cancellationToken);

		if (resolution.IsLeft)
		{
			return new List<IImportPlaylistError> { new UnableToResolvePlaylistItemsError(string.Join("; ", resolution.GetLeft)) };
		}

		return new ImportPlaylistResponse(
			playlist,
			copyItems,
			items.Count,
			resolution.GetRight.ResolvedTracks.Count,
			resolution.GetRight.UnresolvedTracks.Count,
			resolution.GetRight.CreatedAlbums);
	}
}
