using DiscoTrack.Albums;
using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;

namespace DiscoTrack.Tracks;

public static class TrackMatching
{
	// Two tracks at the same place holding different recordings are different tracks, unless anyRecording is set:
	// a single release has one recording per place, so there the place is enough
	public static Track? FindMatch(this IReadOnlyList<Track> tracks, IReadOnlyList<ExternalId> externalIds, int discNumber, int position, string title, bool anyRecording = false)
	{
		var isrcs = externalIds.OfType<Isrc>().ToList();

		// An ISRC names a recording but not which of its tracks, so the ids a provider gives the track itself come first
		return tracks.FirstOrDefault(track => track.ExternalIds.Intersect(externalIds.Except(isrcs)).Any())
			?? tracks.FirstOrDefault(track => track.ExternalIds.Intersect(isrcs).Any())
			?? tracks.FirstOrDefault(track =>
				track.DiscNumber == discNumber
				&& track.Position == position
				&& MatchKey.For(track.Title) == MatchKey.For(title)
				&& (anyRecording || !(HoldsRecording(track.ExternalIds) && HoldsRecording(externalIds))));
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
