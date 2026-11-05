using DiscoTrack.Core.Library;
using DiscoTrack.Infrastructure.Clients.Subsonic;
using DiscoTrack.Infrastructure.Clients.Subsonic.Models;
using DiscoTrack.Infrastructure.Library;
using DiscoTrack.Infrastructure.Tests.Clients.Subsonic;

namespace DiscoTrack.Infrastructure.Tests.Library;

public class SubsonicLibrarySourceTests
{
	private readonly StubSubsonicClient _client = new();

	private SubsonicLibrarySource Source(int pageSize = 500)
	{
		return new(_client, TestSubsonicOptions.Create(pageSize));
	}

	[Fact]
	public async Task GetAlbums_pages_until_a_short_page()
	{
		_client.Albums = [Album("1"), Album("2"), Album("3"), Album("4"), Album("5")];

		var albums = await Source(pageSize: 2).GetAlbums(CancellationToken.None);

		Assert.Equal(["1", "2", "3", "4", "5"], albums.Select(a => a.SubsonicId));
		Assert.Equal([(2, 0), (2, 2), (2, 4)], _client.AlbumRequests);
	}

	[Fact]
	public async Task GetAlbums_stops_after_one_page_when_the_library_fits_in_it()
	{
		_client.Albums = [Album("1"), Album("2")];

		var albums = await Source(pageSize: 500).GetAlbums(CancellationToken.None);

		Assert.Equal(2, albums.Count);
		Assert.Single(_client.AlbumRequests);
	}

	[Fact]
	public async Task GetAlbums_returns_empty_for_an_empty_library()
	{
		_client.Albums = [];

		var albums = await Source().GetAlbums(CancellationToken.None);

		Assert.Empty(albums);
	}

	[Fact]
	public async Task GetAlbums_maps_fields_and_credits_in_order()
	{
		_client.Albums =
		[
			new AlbumID3
			{
				Id = "alb-split", Name = "Split", Year = 2010, IsCompilation = false,
				Artists = [new ArtistID3 { Id = "art-tool", Name = "Tool" }, new ArtistID3 { Id = "art-puscifer", Name = "Puscifer" }],
			},
		];

		var album = (await Source().GetAlbums(CancellationToken.None)).Single();

		Assert.Equal("alb-split", album.SubsonicId);
		Assert.Equal("Split", album.Title);
		Assert.Equal(2010, album.Year);
		Assert.False(album.IsCompilation);
		Assert.Equal([new LibraryArtist("art-tool", "Tool"), new LibraryArtist("art-puscifer", "Puscifer")], album.Artists);
	}

	[Fact]
	public async Task GetAlbums_falls_back_to_artistId_and_artist_when_the_array_is_absent()
	{
		_client.Albums = [new AlbumID3 { Id = "alb-alice", Name = "Alice by the Moon", Year = 2009, ArtistId = "art-checks", Artist = "The Checks" }];

		var album = (await Source().GetAlbums(CancellationToken.None)).Single();

		Assert.Equal([new LibraryArtist("art-checks", "The Checks")], album.Artists);
	}

	[Fact]
	public async Task GetAlbums_leaves_year_null_when_absent()
	{
		_client.Albums = [new AlbumID3 { Id = "alb-untagged", Name = "Untagged" }];

		var album = (await Source().GetAlbums(CancellationToken.None)).Single();

		Assert.Null(album.Year);
	}

	[Fact]
	public async Task GetArtists_flattens_every_index_in_order()
	{
		_client.Indexes =
		[
			new IndexID3 { Name = "A", Artist = [Artist("Avenged Sevenfold")] },
			new IndexID3 { Name = "S", Artist = [Artist("System of a Down"), Artist("Shihad")] },
		];

		var artists = await Source().GetArtists(CancellationToken.None);

		Assert.Equal(["Avenged Sevenfold", "System of a Down", "Shihad"], artists.Select(a => a.Name));
	}

	[Fact]
	public async Task GetArtists_maps_id_name_and_musicbrainz_id()
	{
		_client.Indexes = [new IndexID3 { Name = "P", Artist = [new ArtistID3 { Id = "art-puscifer", Name = "Puscifer", AlbumCount = 14, MusicBrainzId = "6e4277d8-7b64-44a3-b823-13bd15114e22" }] }];

		var artist = (await Source().GetArtists(CancellationToken.None)).Single();

		Assert.Equal(new LibraryArtist("art-puscifer", "Puscifer", "6e4277d8-7b64-44a3-b823-13bd15114e22"), artist);
	}

	[Fact]
	public async Task GetArtists_reports_an_untagged_musicbrainz_id_as_null()
	{
		_client.Indexes = [new IndexID3 { Name = "C", Artist = [new ArtistID3 { Id = "art-checks", Name = "The Checks", AlbumCount = 2, MusicBrainzId = "" }] }];

		var artist = (await Source().GetArtists(CancellationToken.None)).Single();

		Assert.Null(artist.MusicBrainzId);
	}

	private static AlbumID3 Album(string id)
	{
		return new() { Id = id, Name = $"Album {id}", Artists = [Artist("Tool")] };
	}

	private static ArtistID3 Artist(string name)
	{
		return new() { Id = name.ToLowerInvariant().Replace(" ", "-"), Name = name };
	}

	private sealed class StubSubsonicClient : ISubsonicClient
	{
		public IReadOnlyList<AlbumID3> Albums { get; set; } = [];
		public IReadOnlyList<IndexID3> Indexes { get; set; } = [];
		public List<(int Size, int Offset)> AlbumRequests { get; } = [];

		public Task<IReadOnlyList<AlbumID3>> GetAlbumList2(AlbumListType type, int size, int offset, CancellationToken ct)
		{
			AlbumRequests.Add((size, offset));
			return Task.FromResult<IReadOnlyList<AlbumID3>>(Albums.Skip(offset).Take(size).ToList());
		}

		public Task<IReadOnlyList<IndexID3>> GetArtists(CancellationToken ct)
		{
			return Task.FromResult(Indexes);
		}
	}
}
