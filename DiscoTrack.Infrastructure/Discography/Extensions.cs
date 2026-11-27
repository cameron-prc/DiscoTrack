using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

namespace DiscoTrack.Infrastructure.Discography;

internal static class Extensions
{
	public const string VariousArtistsId = "89ad4ac3-39f7-470e-963a-56509c546377";

	public static DiscographyAlbum ToDiscographyAlbum(this ReleaseGroup releaseGroup)
	{
		var credits = releaseGroup.ArtistCredit ?? [];

		return new(
			new MusicBrainzReleaseGroupId(releaseGroup.Id),
			releaseGroup.Title,
			ToYear(releaseGroup.FirstReleaseDate),
			credits.Any(credit => credit.Artist.Id == VariousArtistsId),
			credits.Select(credit => credit.ToDiscographyArtist()).ToList());
	}

	public static IReadOnlyList<DiscographyTrack> ToDiscographyTracks(this Release release)
	{
		return (release.Media ?? [])
			.SelectMany(medium => (medium.Tracks ?? [])
				.Where(track => track.Recording is not null)
				.Select(track => new DiscographyTrack(
					new MusicBrainzRecordingId(track.Recording!.Id),
					track.Title,
					medium.Position,
					track.Position,
					track.Length ?? track.Recording.Length,
					(track.Recording.ArtistCredit ?? []).Select(credit => credit.ToDiscographyArtist()).ToList(),
					(track.Recording.Isrcs ?? []).Where(isrc => !string.IsNullOrWhiteSpace(isrc)).Select(isrc => new Isrc(isrc)).Distinct().ToList())))
			.ToList();
	}

	public static int TrackCount(this Release release)
	{
		return (release.Media ?? []).Sum(medium => medium.TrackCount);
	}

	public static DiscographyArtist ToDiscographyArtist(this ArtistCredit credit)
	{
		return credit.Artist.ToDiscographyArtist();
	}

	public static DiscographyArtist ToDiscographyArtist(this Artist artist)
	{
		return new(new MusicBrainzArtistId(artist.Id), artist.Name, string.IsNullOrEmpty(artist.Disambiguation) ? null : artist.Disambiguation);
	}

	private static int? ToYear(string? date)
	{
		return date is { Length: >= 4 } && int.TryParse(date[..4], out var year) ? year : null;
	}
}
