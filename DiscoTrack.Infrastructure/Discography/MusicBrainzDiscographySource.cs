using System.Text.RegularExpressions;
using DiscoTrack.Discography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.MusicBrainz;
using DiscoTrack.Infrastructure.Clients.MusicBrainz.Models;
using Microsoft.Extensions.Options;

namespace DiscoTrack.Infrastructure.Discography;

public partial class MusicBrainzDiscographySource(IMusicBrainzClient client, IOptions<MusicBrainzOptions> options) : IDiscographySource
{
	private const string OfficialStatus = "Official";

	private static readonly IReadOnlyList<string> StudioAlbumTypes = ["album", "ep"];

	private readonly int _pageSize = options.Value.PageSize;
	private readonly int _searchSize = options.Value.SearchSize;
	private readonly int _maxReleaseGroupPages = options.Value.MaxReleaseGroupPages;
	private readonly int _maxRecordingsPerIsrc = options.Value.MaxRecordingsPerIsrc;

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

	public Provider RecordingProvider
	{
		get { return Provider.MusicBrainzRecording; }
	}

	public ExternalId VariousArtistsId
	{
		get { return new MusicBrainzArtistId(Extensions.VariousArtistsId); }
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

		return release.ReleaseGroup.ToDiscographyAlbum() with { Tracks = release.ToDiscographyTracks() };
	}

	private async Task<DiscographyAlbum?> GetAlbum(MusicBrainzReleaseGroupId releaseGroupId, CancellationToken ct)
	{
		return (await client.GetReleaseGroup(releaseGroupId.Value, ct))?.ToDiscographyAlbum();
	}

	public Task<IReadOnlyList<DiscographyTrack>> GetAlbumTracks(ExternalId id, CancellationToken ct)
	{
		return id switch
		{
			MusicBrainzReleaseId releaseId => GetAlbumTracks(releaseId, ct),
			MusicBrainzReleaseGroupId releaseGroupId => GetAlbumTracks(releaseGroupId, ct),
			_ => throw new ArgumentException($"{id.Provider} is not a MusicBrainz release or release group id", nameof(id)),
		};
	}

	private async Task<IReadOnlyList<DiscographyTrack>> GetAlbumTracks(MusicBrainzReleaseId releaseId, CancellationToken ct)
	{
		return (await client.GetRelease(releaseId.Value, ct))?.ToDiscographyTracks() ?? [];
	}

	// A release group has no tracklist of its own, so take the one of its earliest official release.
	// Releases out on the same date are split by the track count most of the group's releases share.
	private async Task<IReadOnlyList<DiscographyTrack>> GetAlbumTracks(MusicBrainzReleaseGroupId releaseGroupId, CancellationToken ct)
	{
		var releases = await client.BrowseReleases(releaseGroupId.Value, _pageSize, 0, ct);
		var officialReleases = releases.Where(release => release.Status == OfficialStatus).ToList();
		var candidates = officialReleases.Count > 0 ? officialReleases : releases;
		var releasesByTrackCount = candidates.ToLookup(release => release.TrackCount());

		var chosen = candidates
			.OrderBy(release => string.IsNullOrEmpty(release.Date))
			.ThenBy(release => release.Date, StringComparer.Ordinal)
			.ThenByDescending(release => releasesByTrackCount[release.TrackCount()].Count())
			.ThenBy(release => release.Id, StringComparer.Ordinal)
			.FirstOrDefault();

		return chosen is null ? [] : await GetAlbumTracks(new MusicBrainzReleaseId(chosen.Id), ct);
	}

	public async Task<IReadOnlyList<ExternalId>> FindLinkedReleases(ExternalId albumId, CancellationToken ct)
	{
		if (albumId is not SpotifyAlbumId spotifyAlbumId)
		{
			return [];
		}

		var releases = await client.GetReleasesByUrl($"https://open.spotify.com/album/{spotifyAlbumId.Value}", ct);

		return releases
			.Select(release => (ExternalId)new MusicBrainzReleaseId(release.Id))
			.Distinct()
			.ToList();
	}

	public async Task<IReadOnlyList<DiscographyRecording>> FindRecordings(string isrc, CancellationToken ct)
	{
		var recordings = new List<DiscographyRecording>();

		foreach (var found in (await client.GetRecordingsByIsrc(isrc.ToUpperInvariant(), ct)).Take(_maxRecordingsPerIsrc))
		{
			var recording = await client.GetRecording(found.Id, ct);

			recordings.Add(new DiscographyRecording(
				new MusicBrainzRecordingId(found.Id),
				(recording?.Releases ?? [])
					.Select(release => new DiscographyRelease(new MusicBrainzReleaseId(release.Id), release.Title, release.Status == OfficialStatus, release.Date, release.TrackCount()))
					.ToList()));
		}

		return recordings;
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
			.Select(releaseGroup => releaseGroup.ToDiscographyAlbum())
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
			if (releaseGroups.Count >= _pageSize * _maxReleaseGroupPages)
			{
				throw new MusicBrainzException($"Artist {artistId.Value} has more than {releaseGroups.Count} release groups.");
			}

			releaseGroupPage = await client.BrowseReleaseGroups(artistId.Value, StudioAlbumTypes, _pageSize, releaseGroups.Count, ct);
			releaseGroups.AddRange(releaseGroupPage);
		}
		while (releaseGroupPage.Count >= _pageSize);

		return releaseGroups
			.Where(releaseGroup => releaseGroup.SecondaryTypes is null or { Count: 0 })
			.Select(releaseGroup => releaseGroup.ToDiscographyAlbum())
			.ToList();
	}

	public async Task<IReadOnlyList<DiscographyArtist>> SearchArtists(string name, CancellationToken ct)
	{
		var artists = await client.SearchArtists($"artist:\"{Escape(name)}\"", _searchSize, 0, ct);

		return artists
			.Select(artist => artist.ToDiscographyArtist())
			.ToList();
	}

	private static string Escape(string value)
	{
		return LuceneSpecialCharacters().Replace(value, @"\$0");
	}

	[GeneratedRegex("""[+\-!(){}\[\]^"~*?:\\/&|]""")]
	private static partial Regex LuceneSpecialCharacters();
}
