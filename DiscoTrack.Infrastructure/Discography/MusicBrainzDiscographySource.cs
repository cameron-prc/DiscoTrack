using System.Text.RegularExpressions;
using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Discography;

public partial class MusicBrainzDiscographySource(IMusicBrainzClient client, IOptions<MusicBrainzOptions> options) : IDiscographySource
{
	private readonly int _pageSize = options.Value.PageSize;
	private readonly int _searchSize = options.Value.SearchSize;

	public async Task<DiscographyAlbum?> GetAlbum(MusicBrainzReleaseId releaseId, CancellationToken ct)
	{
		var release = await client.GetRelease(releaseId.Value, ct);
		if (release?.ReleaseGroup is null)
		{
			return null;
		}

		return release.ReleaseGroup.ToDiscographyAlbum(await GetReleases(release.ReleaseGroup.Id, ct));
	}

	public async Task<DiscographyAlbum?> GetAlbum(MusicBrainzReleaseGroupId releaseGroupId, CancellationToken ct)
	{
		var releaseGroup = await client.GetReleaseGroup(releaseGroupId.Value, ct);
		if (releaseGroup is null)
		{
			return null;
		}

		return releaseGroup.ToDiscographyAlbum(await GetReleases(releaseGroup.Id, ct));
	}

	public async Task<IReadOnlyList<DiscographyAlbum>> SearchAlbums(MusicBrainzArtistId artistId, string title, CancellationToken ct)
	{
		var releaseGroups = await client.SearchReleaseGroups($"arid:{artistId.Value} AND releasegroup:\"{Escape(title)}\"", _searchSize, 0, ct);

		return releaseGroups
			.Select(releaseGroup => releaseGroup.ToDiscographyAlbum(releaseGroup.Releases ?? []))
			.ToList();
	}

	public async Task<IReadOnlyList<DiscographyArtist>> SearchArtists(string name, CancellationToken ct)
	{
		var artists = await client.SearchArtists($"artist:\"{Escape(name)}\"", _searchSize, 0, ct);

		return artists
			.Select(artist => artist.ToDiscographyArtist())
			.ToList();
	}

	private async Task<IReadOnlyList<Release>> GetReleases(string releaseGroupId, CancellationToken ct)
	{
		var releases = new List<Release>();
		IReadOnlyList<Release> releasePage;

		do
		{
			releasePage = await client.BrowseReleases(releaseGroupId, _pageSize, releases.Count, ct);
			releases.AddRange(releasePage);
		}
		while (releasePage.Count >= _pageSize);

		return releases;
	}

	private static string Escape(string value)
	{
		return LuceneSpecialCharacters().Replace(value, @"\$0");
	}

	[GeneratedRegex("""[+\-!(){}\[\]^"~*?:\\/&|]""")]
	private static partial Regex LuceneSpecialCharacters();
}
