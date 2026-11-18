using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.ExternalIds;

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

	[Fact]
	public void SetExternalId_adds_an_id_from_a_provider_the_album_does_not_have()
	{
		var album = new Album { ExternalIds = [new SubsonicId("alb-lateralus")], Title = "Lateralus", Year = 2001 };

		album.SetExternalId(new MusicBrainzId("mb-lateralus"));

		Assert.Equal([new SubsonicId("alb-lateralus"), new MusicBrainzId("mb-lateralus")], album.ExternalIds);
	}

	[Fact]
	public void SetExternalId_replaces_the_id_from_the_same_provider()
	{
		var album = new Album { ExternalIds = [new SubsonicId("alb-lateralus"), new MusicBrainzId("mb-lateralus")], Title = "Lateralus", Year = 2001 };

		album.SetExternalId(new SubsonicId("alb-lateralus-rescanned"));

		Assert.Equal([new MusicBrainzId("mb-lateralus"), new SubsonicId("alb-lateralus-rescanned")], album.ExternalIds);
	}

	[Fact]
	public void ToString_is_the_primary_artist_title_year_and_id()
	{
		var album = new Album
		{
			Title = "Lateralus",
			Year = 2001,
			Artists = [new AlbumArtist { Artist = new Artist { Name = "Tool" }, Position = 1 }],
		};

		Assert.Equal($"Tool - Lateralus (2001) ({album.Id})", album.ToString());
	}

	[Fact]
	public void ToString_of_a_compilation_uses_various_artists()
	{
		var album = new Album { Title = "Nature's Best", Year = 2002, IsCompilation = true };

		Assert.Equal($"Various Artists - Nature's Best (2002) ({album.Id})", album.ToString());
	}
}
