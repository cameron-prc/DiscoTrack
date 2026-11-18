using DiscoTrack.Artists;
using DiscoTrack.ExternalIds;

namespace DiscoTrack.Tests.Artists;

public class ArtistTests
{
	[Fact]
	public void SetExternalId_adds_an_id_from_a_provider_the_artist_does_not_have()
	{
		var artist = new Artist { ExternalIds = [new SubsonicId("art-tool")], Name = "Tool" };

		artist.SetExternalId(new MusicBrainzId("mb-tool"));

		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzId("mb-tool")], artist.ExternalIds);
	}

	[Fact]
	public void SetExternalId_replaces_the_id_from_the_same_provider()
	{
		var artist = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzId("mb-tool")], Name = "Tool" };

		artist.SetExternalId(new SubsonicId("art-tool-rescanned"));

		Assert.Equal([new MusicBrainzId("mb-tool"), new SubsonicId("art-tool-rescanned")], artist.ExternalIds);
	}

	[Fact]
	public void ToString_is_the_name_and_id()
	{
		var artist = new Artist { Name = "Tool" };

		Assert.Equal($"Tool ({artist.Id})", artist.ToString());
	}
}
