using DiscoTrack.Albums;
using DiscoTrack.Discography.SyncAlbumWithDiscography;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs.JobHandlers;

public class SyncAlbumWithDiscographyJobHandler(ISyncAlbumWithDiscographyService syncAlbumWithDiscography, ILogger<SyncAlbumWithDiscographyJobHandler> logger) : ISyncAlbumWithDiscographyJobHandler
{
	public async Task Execute(AlbumId albumId, CancellationToken cancellationToken)
	{
		var result = await syncAlbumWithDiscography.Run(albumId, cancellationToken);

		if (result.IsLeft)
		{
			foreach (var error in result.GetLeft)
			{
				LogError(albumId, error);
			}
			return;
		}

		var response = result.GetRight;

		logger.LogInformation(
			"Discography sync completed for {Album}. {CreatedArtists} artists created, {UpdatedArtists} artists updated, {LinkedArtists} artists linked, {UnlinkedArtists} unlinked",
			response.Album,
			response.CreatedArtists.Count,
			response.UpdatedArtists.Count,
			response.LinkedArtists.Count,
			response.UnlinkedArtists.Count);

		if (response.AbsorbedAlbum is not null)
		{
			logger.LogInformation("{AbsorbedAlbum} held the same discography album and was merged into {Album}", response.AbsorbedAlbum, response.Album);
		}
	}

	private void LogError(AlbumId albumId, ISyncAlbumWithDiscographyError error)
	{
		switch (error)
		{
			case UnableToConnectToCatalogueError connectToCatalogue:
				logger.LogError("Discography sync for album {AlbumId} failed to read the catalogue: {Message}", albumId, connectToCatalogue.Message);
				break;
			case UnableToFindAlbumError:
				logger.LogError("Discography sync for album {AlbumId} failed: the album is not in the catalogue", albumId);
				break;
			case UnableToConnectToDiscographyError connectToDiscography:
				logger.LogError("Discography sync for album {AlbumId} failed to read the discography: {Message}", albumId, connectToDiscography.Message);
				break;
			case UnableToFindDiscographyAlbumError:
				logger.LogError("Discography sync for album {AlbumId} failed: the discography has no matching album", albumId);
				break;
			case UnableToSaveArtistError saveArtist:
				logger.LogError("Discography sync for album {AlbumId} failed to save an artist: {Message}", albumId, saveArtist.Message);
				break;
			case UnableToSaveAlbumError saveAlbum:
				logger.LogError("Discography sync for album {AlbumId} failed to save the album: {Message}", albumId, saveAlbum.Message);
				break;
			default:
				logger.LogError("Discography sync for album {AlbumId} failed: {Error}", albumId, error);
				break;
		}
	}
}
