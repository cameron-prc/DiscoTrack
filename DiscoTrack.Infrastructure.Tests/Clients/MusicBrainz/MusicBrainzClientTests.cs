using System.Net;
using System.Web;
using DiscoTrack.Infrastructure.Clients.MusicBrainz;

namespace DiscoTrack.Infrastructure.Tests.Clients.MusicBrainz;

public class MusicBrainzClientTests
{
	private const string ToolId = "66fc5bf8-daa4-4241-b378-9bc9077939d2";
	private const string LateralusReleaseId = "ea5777f4-867b-4ce2-bfa9-8f3a2239812d";
	private const string LateralusReleaseGroupId = "02f79295-21e1-34cc-82f2-63219eec4f0a";

	private readonly StubHttpHandler _http = new();

	private MusicBrainzClient Client()
	{
		return new(new HttpClient(_http) { BaseAddress = new Uri(TestMusicBrainzOptions.BaseUrl) });
	}

	[Fact]
	public async Task GetRelease_requests_the_release_with_its_group_and_credits()
	{
		_http.WhenPathContains("release/", Fixtures.Read("musicbrainz/release.json"));

		await Client().GetRelease(LateralusReleaseId, CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		Assert.Equal($"/ws/2/release/{LateralusReleaseId}", request.AbsolutePath);
		Assert.Equal("?inc=release-groups+artist-credits&fmt=json", request.Query);
	}

	[Fact]
	public async Task GetRelease_returns_the_release_as_deserialised()
	{
		_http.WhenPathContains("release/", Fixtures.Read("musicbrainz/release.json"));

		var release = await Client().GetRelease(LateralusReleaseId, CancellationToken.None);

		Assert.NotNull(release);
		Assert.Equal(LateralusReleaseId, release.Id);
		Assert.Equal("Lateralus", release.Title);
		Assert.Equal(LateralusReleaseGroupId, release.ReleaseGroup!.Id);
		Assert.Equal("2001-05-14", release.ReleaseGroup.FirstReleaseDate);
		var credit = Assert.Single(release.ReleaseGroup.ArtistCredit!);
		Assert.Equal(ToolId, credit.Artist.Id);
		Assert.Equal("American rock band", credit.Artist.Disambiguation);
	}

	[Fact]
	public async Task GetRelease_returns_null_when_the_release_is_not_found()
	{
		_http.WhenPathContains("release/", Fixtures.Read("musicbrainz/error-not-found.json"), HttpStatusCode.NotFound);

		var release = await Client().GetRelease(LateralusReleaseId, CancellationToken.None);

		Assert.Null(release);
	}

	[Fact]
	public async Task GetReleaseGroup_requests_the_release_group_with_its_credits()
	{
		_http.WhenPathContains("release-group/", Fixtures.Read("musicbrainz/release-group.json"));

		await Client().GetReleaseGroup(LateralusReleaseGroupId, CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		Assert.Equal($"/ws/2/release-group/{LateralusReleaseGroupId}", request.AbsolutePath);
		Assert.Equal("?inc=artist-credits&fmt=json", request.Query);
	}

	[Fact]
	public async Task GetReleaseGroup_returns_the_release_group_as_deserialised()
	{
		_http.WhenPathContains("release-group/", Fixtures.Read("musicbrainz/release-group.json"));

		var releaseGroup = await Client().GetReleaseGroup(LateralusReleaseGroupId, CancellationToken.None);

		Assert.NotNull(releaseGroup);
		Assert.Equal(LateralusReleaseGroupId, releaseGroup.Id);
		Assert.Equal("Lateralus", releaseGroup.Title);
		Assert.Equal("2001-05-14", releaseGroup.FirstReleaseDate);
		Assert.Equal("Tool", Assert.Single(releaseGroup.ArtistCredit!).Artist.Name);
	}

	[Fact]
	public async Task GetReleaseGroup_returns_null_when_the_release_group_is_not_found()
	{
		_http.WhenPathContains("release-group/", Fixtures.Read("musicbrainz/error-not-found.json"), HttpStatusCode.NotFound);

		var releaseGroup = await Client().GetReleaseGroup(LateralusReleaseGroupId, CancellationToken.None);

		Assert.Null(releaseGroup);
	}

	[Fact]
	public async Task BrowseReleases_requests_the_given_release_group_limit_and_offset()
	{
		_http.WhenPathContains("release?", Fixtures.Read("musicbrainz/release-browse.json"));

		await Client().BrowseReleases(LateralusReleaseGroupId, limit: 3, offset: 6, CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		var query = HttpUtility.ParseQueryString(request.Query);
		Assert.Equal("/ws/2/release", request.AbsolutePath);
		Assert.Equal(LateralusReleaseGroupId, query["release-group"]);
		Assert.Equal("3", query["limit"]);
		Assert.Equal("6", query["offset"]);
		Assert.Equal("json", query["fmt"]);
	}

	[Fact]
	public async Task BrowseReleases_returns_the_page_as_deserialised()
	{
		_http.WhenPathContains("release?", Fixtures.Read("musicbrainz/release-browse.json"));

		var releases = await Client().BrowseReleases(LateralusReleaseGroupId, 3, 0, CancellationToken.None);

		Assert.Equal(3, releases.Count);
		Assert.Equal("451df9bc-1ba2-453c-9c60-9537a5f3c6e3", releases[0].Id);
		Assert.All(releases, release => Assert.Equal("Lateralus", release.Title));
	}

	[Fact]
	public async Task SearchReleaseGroups_requests_the_given_query_limit_and_offset()
	{
		_http.WhenPathContains("release-group?", Fixtures.Read("musicbrainz/release-group-search.json"));

		await Client().SearchReleaseGroups($"arid:{ToolId} AND releasegroup:\"Lateralus\"", limit: 25, offset: 0, CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		var query = HttpUtility.ParseQueryString(request.Query);
		Assert.Equal("/ws/2/release-group", request.AbsolutePath);
		Assert.Equal($"arid:{ToolId} AND releasegroup:\"Lateralus\"", query["query"]);
		Assert.Equal("25", query["limit"]);
		Assert.Equal("0", query["offset"]);
		Assert.Equal("json", query["fmt"]);
	}

	[Fact]
	public async Task SearchReleaseGroups_returns_the_matches_as_deserialised()
	{
		_http.WhenPathContains("release-group?", Fixtures.Read("musicbrainz/release-group-search.json"));

		var releaseGroups = await Client().SearchReleaseGroups("releasegroup:\"Lateralus\"", 25, 0, CancellationToken.None);

		Assert.Equal(2, releaseGroups.Count);
		var album = releaseGroups.Single(releaseGroup => releaseGroup.Id == LateralusReleaseGroupId);
		Assert.Equal("2001-05-14", album.FirstReleaseDate);
		Assert.Equal(ToolId, Assert.Single(album.ArtistCredit!).Artist.Id);
		Assert.Equal(
			[LateralusReleaseId, "7cd19f01-41b1-4e13-9b16-064938ac52ba", "e6f4d5e3-9b27-4f54-a7f6-0f7f76ed1485"],
			album.Releases!.Select(release => release.Id));
	}

	[Fact]
	public async Task SearchArtists_requests_the_given_query_limit_and_offset()
	{
		_http.WhenPathContains("artist?", Fixtures.Read("musicbrainz/artist-search.json"));

		await Client().SearchArtists("artist:\"Tool\"", limit: 25, offset: 0, CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		var query = HttpUtility.ParseQueryString(request.Query);
		Assert.Equal("/ws/2/artist", request.AbsolutePath);
		Assert.Equal("artist:\"Tool\"", query["query"]);
		Assert.Equal("25", query["limit"]);
		Assert.Equal("0", query["offset"]);
		Assert.Equal("json", query["fmt"]);
	}

	[Fact]
	public async Task SearchArtists_returns_the_matches_as_deserialised()
	{
		_http.WhenPathContains("artist?", Fixtures.Read("musicbrainz/artist-search.json"));

		var artists = await Client().SearchArtists("artist:\"Tool\"", 25, 0, CancellationToken.None);

		Assert.Equal(3, artists.Count);
		Assert.Equal(ToolId, artists[0].Id);
		Assert.Equal("Tool", artists[0].Name);
		Assert.Equal("American rock band", artists[0].Disambiguation);
	}

	[Fact]
	public async Task Any_call_throws_with_the_server_message_when_the_status_is_not_successful()
	{
		_http.WhenPathContains("artist?", Fixtures.Read("musicbrainz/error-busy.json"), HttpStatusCode.ServiceUnavailable);

		var ex = await Assert.ThrowsAsync<MusicBrainzException>(() => Client().SearchArtists("artist:\"Tool\"", 25, 0, CancellationToken.None));

		Assert.Equal("The MusicBrainz web server is currently busy. Please try again later.", ex.Message);
	}

	[Fact]
	public async Task Any_call_throws_with_the_status_when_the_error_body_is_not_json()
	{
		_http.WhenPathContains("artist?", "<html>Bad Gateway</html>", HttpStatusCode.BadGateway);

		var ex = await Assert.ThrowsAsync<MusicBrainzException>(() => Client().SearchArtists("artist:\"Tool\"", 25, 0, CancellationToken.None));

		Assert.Equal("MusicBrainz returned status 502 with no message.", ex.Message);
	}

	[Fact]
	public async Task Any_call_throws_when_the_body_is_null()
	{
		_http.WhenPathContains("release/", "null");

		var ex = await Assert.ThrowsAsync<MusicBrainzException>(() => Client().GetRelease(LateralusReleaseId, CancellationToken.None));

		Assert.Equal("Empty response from MusicBrainz.", ex.Message);
	}
}
