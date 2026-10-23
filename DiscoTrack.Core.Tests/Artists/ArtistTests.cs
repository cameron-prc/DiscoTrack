using DiscoTrack.Core.Albums;
using DiscoTrack.Core.Artists;

namespace DiscoTrack.Core.Tests.Artists;

public class ArtistTests
{
	[Fact]
	public void ArtistRef_Albums_throws()
	{
		var artist = new ArtistRef { Name = "Shihad" };

		Assert.Throws<InvalidOperationException>(() => artist.Albums);
	}

	[Fact]
	public void ArtistRef_AddAlbum_throws()
	{
		var artist = new ArtistRef { Name = "Shihad" };
		var album = new Album { Title = "Killjoy", Year = 1995 };

		Assert.Throws<InvalidOperationException>(() => artist.AddAlbum(album));
	}

	[Fact]
	public void ArtistFull_AddAlbum_completes_the_credit_on_the_album()
	{
		var artist = new ArtistFull { Name = "Shihad" };
		var album = new Album { Title = "Killjoy", Year = 1995 };

		artist.AddAlbum(album);

		Assert.Equal(new[] { album }, artist.Albums);
		Assert.Same(artist, Assert.Single(album.Artists).Artist);
		Assert.Same(artist, album.PrimaryArtist());
	}

	[Fact]
	public void ArtistFull_AddAlbum_is_idempotent()
	{
		var artist = new ArtistFull { Name = "Shihad" };
		var album = new Album { Title = "Killjoy", Year = 1995 };

		artist.AddAlbum(album);
		artist.AddAlbum(album);

		Assert.Single(artist.Albums);
		Assert.Single(album.Artists);
	}

	[Fact]
	public void ArtistFull_AddAlbum_appends_after_existing_credits()
	{
		var primary = new ArtistRef { Name = "Shihad" };
		var guest = new ArtistFull { Name = "Guest" };
		var album = new Album { Title = "Split", Year = 2010 };
		album.AddArtist(primary);

		guest.AddAlbum(album);

		Assert.Equal(new (Artist, int)[] { (primary, 1), (guest, 2) }, album.Artists.Select(a => (a.Artist, a.Position)));
		Assert.Same(primary, album.PrimaryArtist());
	}
}
