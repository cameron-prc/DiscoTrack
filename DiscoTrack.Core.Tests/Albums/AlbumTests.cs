using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;

namespace DiscoTrack.Core.Tests.Albums;

public class AlbumTests
{
	[Fact]
	public void AddArtist_assigns_positions_in_call_order()
	{
		var first = new ArtistRef { Name = "First" };
		var second = new ArtistRef { Name = "Second" };
		var album = new Album { Title = "Split", Year = 2010 };

		album.AddArtist(first);
		album.AddArtist(second);

		Assert.Equal(new (Artist, int)[] { (first, 1), (second, 2) }, album.Artists.Select(a => (a.Artist, a.Position)));
	}

	[Fact]
	public void AddArtist_ignores_an_artist_already_credited()
	{
		var artist = new ArtistRef { Name = "Shihad" };
		var album = new Album { Title = "Killjoy", Year = 1995 };

		album.AddArtist(artist);
		album.AddArtist(artist);

		Assert.Single(album.Artists);
	}

	[Fact]
	public void AddArtist_completes_the_link_for_a_full_artist()
	{
		var artist = new ArtistFull { Name = "Shihad" };
		var album = new Album { Title = "Killjoy", Year = 1995 };

		album.AddArtist(artist);

		Assert.Equal(new[] { album }, artist.Albums);
	}

	[Fact]
	public void AddArtist_does_not_touch_a_ref()
	{
		var artist = new ArtistRef { Name = "Shihad" };
		var album = new Album { Title = "Killjoy", Year = 1995 };

		Assert.Null(Record.Exception(() => album.AddArtist(artist)));
		Assert.Same(artist, Assert.Single(album.Artists).Artist);
	}

	[Fact]
	public void PrimaryArtist_is_first_added()
	{
		var primary = new ArtistRef { Name = "Shihad" };
		var guest = new ArtistRef { Name = "Guest" };
		var album = new Album { Title = "Split", Year = 2010 };
		album.AddArtist(primary);
		album.AddArtist(guest);

		Assert.Same(primary, album.PrimaryArtist());
	}

	[Fact]
	public void PrimaryArtist_is_null_for_compilation()
	{
		var various = new ArtistRef { Name = "Various Artists" };
		var album = new Album { Title = "Nature's Best", Year = 2002, IsCompilation = true };
		album.AddArtist(various);

		Assert.Null(album.PrimaryArtist());
	}

	[Fact]
	public void PrimaryArtist_is_null_when_no_artists()
	{
		var album = new Album { Title = "Untagged", Year = 2000 };

		Assert.Null(album.PrimaryArtist());
	}
}
