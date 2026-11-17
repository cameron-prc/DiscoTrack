using DiscoTrack.Library.SyncLibrary;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs.JobHandlers;

public class SyncLibraryJobHandler(SyncLibrary syncLibrary, ILogger<SyncLibraryJobHandler> logger) : ISyncLibraryHandler
{
	public async Task Execute(CancellationToken cancellationToken)
	{
		var result = await syncLibrary.Run(cancellationToken);

		if (result.IsLeft)
		{
			foreach (var error in result.GetLeft)
			{
				LogError(error);
			}
			return;
		}

		var response = result.GetRight;

		foreach (var unprocessedAlbum in response.UnprocessedAlbums)
		{
			logger.LogWarning("Failed to import {Title} ({Year}) from library", unprocessedAlbum.Title, unprocessedAlbum.Year);
		}

		logger.LogInformation(
			"Library sync completed. {ArtistCount} new artists, {AlbumCount} new albums, {SkippedCount} skipped",
			response.NewArtists.Count,
			response.NewAlbums.Count,
			response.UnprocessedAlbums.Count);
	}

	private void LogError(ISyncLibraryError error)
	{
		switch (error)
		{
			case FailedToReadCatalogueError readCatalogue:
				logger.LogError("Library sync failed to fetch artists/albums from catalogue: {Message}", readCatalogue.Message);
				break;
			case FailedToReadLibraryError readLibrary:
				logger.LogError("Library sync failed to fetch albums from library: {Message}", readLibrary.Message);
				break;
			case FailedToSaveCatalogueError saveCatalogue:
				logger.LogError("Library sync failed to update catalogue: {Message}", saveCatalogue.Message);
				break;
			default:
				logger.LogError("Library sync failed: {Error}", error);
				break;
		}
	}
}
