using DiscoTrack.Artists;
using DiscoTrack.Discography.SyncArtistWithDiscography;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs.JobHandlers;

public class SyncArtistWithDiscographyJobHandler(ISyncArtistWithDiscographyService syncArtistWithDiscography, ILogger<SyncArtistWithDiscographyJobHandler> logger) : ISyncArtistWithDiscographyJobHandler
{
	public async Task Execute(ArtistId artistId, CancellationToken cancellationToken)
	{
		var result = await syncArtistWithDiscography.Run(artistId, cancellationToken);

		if (result.IsLeft)
		{
			foreach (var error in result.GetLeft)
			{
				LogError(artistId, error);
			}
			return;
		}

		var response = result.GetRight;

		logger.LogInformation(
			"Discography sync completed for {Artist}. {SyncedAlbums} albums synced, {UnsyncedAlbums} albums not synced, {CreatedAlbums} albums created, {CreatedArtists} artists created, {SkippedAlbums} discography albums skipped",
			response.Artist,
			response.SyncedAlbums.Count,
			response.UnsyncedAlbums.Count,
			response.CreatedAlbums.Count,
			response.CreatedArtists.Count,
			response.SkippedAlbums.Count);

		if (response.AbsorbedArtist is not null)
		{
			logger.LogInformation("{AbsorbedArtist} held the same discography artist and was merged into {Artist}", response.AbsorbedArtist, response.Artist);
		}
	}

	private void LogError(ArtistId artistId, ISyncArtistWithDiscographyError error)
	{
		switch (error)
		{
			case UnableToConnectToCatalogueError connectToCatalogue:
				logger.LogError("Discography sync for artist {ArtistId} failed to read the catalogue: {Message}", artistId, connectToCatalogue.Message);
				break;
			case UnableToFindArtistError:
				logger.LogError("Discography sync for artist {ArtistId} failed: the artist is not in the catalogue", artistId);
				break;
			case UnableToConnectToDiscographyError connectToDiscography:
				logger.LogError("Discography sync for artist {ArtistId} failed to read the discography: {Message}", artistId, connectToDiscography.Message);
				break;
			case UnableToFindDiscographyArtistError:
				logger.LogError("Discography sync for artist {ArtistId} failed: the discography has no matching artist", artistId);
				break;
			case UnableToSaveArtistError saveArtist:
				logger.LogError("Discography sync for artist {ArtistId} failed to save an artist: {Message}", artistId, saveArtist.Message);
				break;
			case UnableToSaveAlbumError saveAlbum:
				logger.LogError("Discography sync for artist {ArtistId} failed to save an album: {Message}", artistId, saveAlbum.Message);
				break;
			default:
				logger.LogError("Discography sync for artist {ArtistId} failed: {Error}", artistId, error);
				break;
		}
	}
}
