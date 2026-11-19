using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;

namespace DiscoTrack.Infrastructure.Discography;

internal static class Extensions
{
	public static DiscographyAlbum ToDiscographyAlbum(this ReleaseGroup releaseGroup, IReadOnlyList<Release> releases)
	{
		return new(
			new MusicBrainzReleaseGroupId(releaseGroup.Id),
			releases.Select(release => new MusicBrainzReleaseId(release.Id)).ToList(),
			releaseGroup.Title,
			ToYear(releaseGroup.FirstReleaseDate),
			(releaseGroup.ArtistCredit ?? []).Select(credit => credit.ToDiscographyArtist()).ToList());
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
