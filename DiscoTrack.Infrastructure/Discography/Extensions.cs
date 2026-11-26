using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

namespace DiscoTrack.Infrastructure.Discography;

internal static class Extensions
{
	private const string VariousArtistsId = "89ad4ac3-39f7-470e-963a-56509c546377";

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
