using System.Text.RegularExpressions;
using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Discography;

public partial class MusicBrainzDiscographySource(IMusicBrainzClient client, IOptions<MusicBrainzOptions> options) : IDiscographySource
{
	private static readonly IReadOnlyList<string> StudioAlbumTypes = ["album", "ep"];

	private readonly int _pageSize = options.Value.PageSize;
	private readonly int _searchSize = options.Value.SearchSize;

	public Provider ReleaseProvider
	{
		get { return Provider.MusicBrainzRelease; }
	}

	public Provider ReleaseGroupProvider
	{
		get { return Provider.MusicBrainzReleaseGroup; }
	}

	public Provider ArtistProvider
	{
		get { return Provider.MusicBrainzArtist; }
	}

	public Task<DiscographyAlbum?> GetAlbum(ExternalId id, CancellationToken ct)
	{
		return id switch
		{
			MusicBrainzReleaseId releaseId => GetAlbum(releaseId, ct),
			MusicBrainzReleaseGroupId releaseGroupId => GetAlbum(releaseGroupId, ct),
			_ => throw new ArgumentException($"{id.Provider} is not a MusicBrainz release or release group id", nameof(id)),
		};
	}

	private async Task<DiscographyAlbum?> GetAlbum(MusicBrainzReleaseId releaseId, CancellationToken ct)
	{
		var release = await client.GetRelease(releaseId.Value, ct);
		if (release?.ReleaseGroup is null)
		{
			return null;
		}

		return release.ReleaseGroup.ToDiscographyAlbum(await GetReleases(release.ReleaseGroup.Id, ct));
	}

	private async Task<DiscographyAlbum?> GetAlbum(MusicBrainzReleaseGroupId releaseGroupId, CancellationToken ct)
	{
		var releaseGroup = await client.GetReleaseGroup(releaseGroupId.Value, ct);
		if (releaseGroup is null)
		{
			return null;
		}

		return releaseGroup.ToDiscographyAlbum(await GetReleases(releaseGroup.Id, ct));
	}

	public Task<DiscographyArtist?> GetArtist(ExternalId id, CancellationToken ct)
	{
		return id switch
		{
			MusicBrainzArtistId artistId => GetArtist(artistId, ct),
			_ => throw new ArgumentException($"{id.Provider} is not a MusicBrainz artist id", nameof(id)),
		};
	}

	private async Task<DiscographyArtist?> GetArtist(MusicBrainzArtistId artistId, CancellationToken ct)
	{
		return (await client.GetArtist(artistId.Value, ct))?.ToDiscographyArtist();
	}

	public Task<IReadOnlyList<DiscographyAlbum>> SearchAlbums(ExternalId artistId, string title, CancellationToken ct)
	{
		return artistId switch
		{
			MusicBrainzArtistId musicBrainzArtistId => SearchAlbums(musicBrainzArtistId, title, ct),
			_ => throw new ArgumentException($"{artistId.Provider} is not a MusicBrainz artist id", nameof(artistId)),
		};
	}

	private async Task<IReadOnlyList<DiscographyAlbum>> SearchAlbums(MusicBrainzArtistId artistId, string title, CancellationToken ct)
	{
		var releaseGroups = await client.SearchReleaseGroups($"arid:{artistId.Value} AND releasegroup:\"{Escape(title)}\"", _searchSize, 0, ct);

		return releaseGroups
			.Select(releaseGroup => releaseGroup.ToDiscographyAlbum(releaseGroup.Releases ?? []))
			.ToList();
	}

	public Task<IReadOnlyList<DiscographyAlbum>> GetArtistAlbums(ExternalId artistId, CancellationToken ct)
	{
		return artistId switch
		{
			MusicBrainzArtistId musicBrainzArtistId => GetArtistAlbums(musicBrainzArtistId, ct),
			_ => throw new ArgumentException($"{artistId.Provider} is not a MusicBrainz artist id", nameof(artistId)),
		};
	}

	private async Task<IReadOnlyList<DiscographyAlbum>> GetArtistAlbums(MusicBrainzArtistId artistId, CancellationToken ct)
	{
		var releaseGroups = new List<ReleaseGroup>();
		IReadOnlyList<ReleaseGroup> releaseGroupPage;

		do
		{
			releaseGroupPage = await client.BrowseReleaseGroups(artistId.Value, StudioAlbumTypes, _pageSize, releaseGroups.Count, ct);
			releaseGroups.AddRange(releaseGroupPage);
		}
		while (releaseGroupPage.Count >= _pageSize);

		return releaseGroups
			.Where(releaseGroup => releaseGroup.SecondaryTypes is null or { Count: 0 })
			.Select(releaseGroup => releaseGroup.ToDiscographyAlbum([]))
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
