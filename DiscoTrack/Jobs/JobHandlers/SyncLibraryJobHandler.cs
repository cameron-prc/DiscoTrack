using DiscoTrack.Artists;
using DiscoTrack.Albums;
using DiscoTrack.Library;
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

		logger.LogInformation(
			"Library sync completed. {ArtistCount} artists and {AlbumCount} albums imported",
			response.Artists.Count,
			response.Albums.Count);

		if (response.Failures.Any())
		{
			logger.LogWarning(
				"Import failed for {FailedArtistCount} artists and {FailedAlbumCount} albums",
				response.Failures.Count(failure => failure.Entity is Artist),
				response.Failures.Count(failure => failure.Entity is Album or LibraryAlbum));
		}
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
			default:
				logger.LogError("Library sync failed: {Error}", error);
				break;
		}
	}
}
