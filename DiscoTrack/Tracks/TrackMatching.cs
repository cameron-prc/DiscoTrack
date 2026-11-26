using DiscoTrack.Albums;
using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;

namespace DiscoTrack.Tracks;

public static class TrackMatching
{
	public static Track? FindMatch(this IReadOnlyList<Track> tracks, IReadOnlyList<ExternalId> externalIds, int discNumber, int position, string title)
	{
		return tracks.FirstOrDefault(track => track.ExternalIds.Intersect(externalIds).Any())
			?? tracks.FirstOrDefault(track =>
				track.DiscNumber == discNumber
				&& track.Position == position
				&& MatchKey.For(track.Title) == MatchKey.For(title)
				&& !(HoldsRecording(track.ExternalIds) && HoldsRecording(externalIds)));
	}

	public static IReadOnlyList<Track> MergeInto(this IReadOnlyList<Track> absorbedTracks, AlbumId albumId, IReadOnlyList<Track> albumTracks)
	{
		var tracks = albumTracks.ToList();

		foreach (var absorbedTrack in absorbedTracks)
		{
			var track = tracks.FindMatch(absorbedTrack.ExternalIds, absorbedTrack.DiscNumber, absorbedTrack.Position, absorbedTrack.Title);

			if (track is null)
			{
				tracks.Add(new Track
				{
					Id = absorbedTrack.Id,
					AlbumId = albumId,
					ExternalIds = absorbedTrack.ExternalIds,
					Title = absorbedTrack.Title,
					DiscNumber = absorbedTrack.DiscNumber,
					Position = absorbedTrack.Position,
					DurationMs = absorbedTrack.DurationMs,
					Artists = absorbedTrack.Artists,
				});
			}
			else
			{
				track.ExternalIds = track.ExternalIds.Union(absorbedTrack.ExternalIds).ToList();
			}
		}

		return tracks;
	}

	private static bool HoldsRecording(IReadOnlyList<ExternalId> externalIds)
	{
		return externalIds.Any(externalId => externalId.Provider == Provider.MusicBrainzRecording);
	}
}
