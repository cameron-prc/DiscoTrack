using System.Web;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Clients.Subsonic.Models;

namespace DiscoTrack.Infrastructure.Tests.Clients.Subsonic;

public class SubsonicClientTests
{
	private readonly StubHttpHandler _http = new();

	private SubsonicClient Client()
	{
		return new(new HttpClient(_http) { BaseAddress = new Uri(TestSubsonicOptions.BaseUrl) }, TestSubsonicOptions.Create());
	}

	[Fact]
	public async Task GetAlbumList2_requests_the_given_type_size_and_offset()
	{
		_http.WhenPathContains("getAlbumList2", Fixtures.Read("subsonic/albumList2-page2.json"));

		await Client().GetAlbumList2(AlbumListType.AlphabeticalByName, size: 4, offset: 4, CancellationToken.None);

		var query = HttpUtility.ParseQueryString(_http.Requests.Single().RequestUri!.Query);
		Assert.Equal("alphabeticalByName", query["type"]);
		Assert.Equal("4", query["size"]);
		Assert.Equal("4", query["offset"]);
	}

	[Fact]
	public async Task GetAlbumList2_returns_the_page_as_deserialised()
	{
		_http.WhenPathContains("getAlbumList2", Fixtures.Read("subsonic/albumList2.json"));

		var page = await Client().GetAlbumList2(AlbumListType.AlphabeticalByName, 500, 0, CancellationToken.None);

		Assert.Equal(4, page.Count);
		var legacy = page.Single(a => a.Name == "Alice by the Moon");
		Assert.Null(legacy.Artists);
		Assert.Equal("1TeigLa2FBy3jtyksTv2y6", legacy.ArtistId);
		Assert.Equal("The Checks", legacy.Artist);
	}

	[Fact]
	public async Task GetAlbumList2_returns_empty_when_the_library_is_empty()
	{
		_http.WhenPathContains("getAlbumList2", Fixtures.Read("subsonic/albumList2-empty.json"));

		var page = await Client().GetAlbumList2(AlbumListType.AlphabeticalByName, 500, 0, CancellationToken.None);

		Assert.Empty(page);
	}

	[Fact]
	public async Task GetArtists_returns_the_indexes_as_deserialised()
	{
		_http.WhenPathContains("getArtists", Fixtures.Read("subsonic/artists.json"));

		var indexes = await Client().GetArtists(CancellationToken.None);

		Assert.Equal(["A", "C", "K", "N", "P", "S", "T"], indexes.Select(i => i.Name));
		var puscifer = indexes.Single(i => i.Name == "P").Artist.Single();
		Assert.Equal("3BCOoHVnn287SKIxXFdoZy", puscifer.Id);
		Assert.Equal(14, puscifer.AlbumCount);
		Assert.Equal("6e4277d8-7b64-44a3-b823-13bd15114e22", puscifer.MusicBrainzId);
	}

	[Fact]
	public async Task GetArtists_keeps_an_empty_musicbrainz_id_as_reported()
	{
		_http.WhenPathContains("getArtists", Fixtures.Read("subsonic/artists.json"));

		var indexes = await Client().GetArtists(CancellationToken.None);

		Assert.Equal("", indexes.SelectMany(i => i.Artist).Single(a => a.Name == "The Checks").MusicBrainzId);
	}

	[Fact]
	public async Task Any_call_throws_with_the_server_message_when_status_is_failed()
	{
		_http.WhenPathContains("getArtists", Fixtures.Read("subsonic/error-auth.json"));

		var ex = await Assert.ThrowsAsync<SubsonicException>(() => Client().GetArtists(CancellationToken.None));

		Assert.Equal("Wrong username or password", ex.Message);
	}

	[Fact]
	public async Task Any_call_throws_when_the_body_is_null()
	{
		_http.WhenPathContains("getArtists", "null");

		var ex = await Assert.ThrowsAsync<SubsonicException>(() => Client().GetArtists(CancellationToken.None));

		Assert.Equal("Empty response from Subsonic.", ex.Message);
	}

	[Fact]
	public async Task Any_call_carries_auth_version_client_and_format_parameters()
	{
		_http.WhenPathContains("getArtists", Fixtures.Read("subsonic/artists.json"));

		await Client().GetArtists(CancellationToken.None);

		var request = _http.Requests.Single().RequestUri!;
		var query = HttpUtility.ParseQueryString(request.Query);
		Assert.Equal("/rest/getArtists", request.AbsolutePath);
		Assert.Equal(TestSubsonicOptions.Username, query["u"]);
		Assert.Equal("1.16.1", query["v"]);
		Assert.Equal("DiscoTrack", query["c"]);
		Assert.Equal("json", query["f"]);
		Assert.Equal(TestSubsonicOptions.Salt, query["s"]);
		Assert.Equal(TestSubsonicOptions.Token, query["t"]);
	}
}
