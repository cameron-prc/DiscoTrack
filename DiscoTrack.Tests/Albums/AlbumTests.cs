using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Tests.Albums;

public class AlbumTests
{
	[Fact]
	public void PrimaryArtist_is_the_lowest_position()
	{
		var primary = new Artist { Name = "Shihad" };
		var guest = new Artist { Name = "Guest" };
		var album = new Album
		{
			Title = "Split",
			Year = 2010,
			Artists = [new AlbumArtist { Artist = guest, Position = 2 }, new AlbumArtist { Artist = primary, Position = 1 }],
		};

		Assert.Same(primary, album.PrimaryArtist());
	}

	[Fact]
	public void PrimaryArtist_is_null_for_compilation()
	{
		var various = new Artist { Name = "Various Artists" };
		var album = new Album
		{
			Title = "Nature's Best",
			Year = 2002,
			IsCompilation = true,
			Artists = [new AlbumArtist { Artist = various, Position = 1 }],
		};

		Assert.Null(album.PrimaryArtist());
	}

	[Fact]
	public void PrimaryArtist_is_null_when_no_artists()
	{
		var album = new Album { Title = "Untagged", Year = 2000 };

		Assert.Null(album.PrimaryArtist());
	}
}
