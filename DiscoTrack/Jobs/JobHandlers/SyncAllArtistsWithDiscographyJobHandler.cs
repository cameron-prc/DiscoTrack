using DiscoTrack.Catalogue;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Jobs.JobHandlers;

public class SyncAllArtistsWithDiscographyJobHandler(
	ICatalogue catalogue,
	ISyncArtistWithDiscographyJobHandler syncArtistWithDiscography,
	ILogger<SyncAllArtistsWithDiscographyJobHandler> logger) : ISyncAllArtistsWithDiscographyJobHandler
{
	public async Task Execute(CancellationToken cancellationToken)
	{
		var artists = (await catalogue.GetArtists(cancellationToken))
				.Where(artist => artist.Tracked)
				.ToList();
		var failedArtists = 0;

		foreach (var artist in artists)
		{
			try
			{
				await syncArtistWithDiscography.Execute(artist.Id, cancellationToken);
			}
			catch (JobFailedException)
			{
				failedArtists++;
			}
		}

		logger.LogInformation(
			"Discography sync completed for all artists. {SyncedArtists} artists synced, {FailedArtists} artists failed",
			artists.Count - failedArtists,
			failedArtists);
	}
}
