using DiscoTrack.Artists;
using DiscoTrack.ExternalIds;

namespace DiscoTrack.Tests.Artists;

public class ArtistTests
{
	[Fact]
	public void SetExternalIds_adds_an_id_from_a_provider_the_artist_does_not_have()
	{
		var artist = new Artist { ExternalIds = [new SubsonicId("art-tool")], Name = "Tool" };

		artist.SetExternalIds([new MusicBrainzArtistId("mb-tool")]);

		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], artist.ExternalIds);
	}

	[Fact]
	public void SetExternalIds_replaces_the_ids_from_the_same_provider()
	{
		var artist = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };

		artist.SetExternalIds([new SubsonicId("art-tool-rescanned")]);

		Assert.Equal([new MusicBrainzArtistId("mb-tool"), new SubsonicId("art-tool-rescanned")], artist.ExternalIds);
	}

	[Fact]
	public void SetExternalIds_holds_several_ids_from_one_provider()
	{
		var artist = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };

		artist.SetExternalIds([new SubsonicId("art-tool"), new SubsonicId("art-tool-dupe")]);

		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool"), new SubsonicId("art-tool-dupe")], artist.ExternalIds);
	}

	[Fact]
	public void SetExternalIds_leaves_the_ids_as_they_were_when_nothing_changed()
	{
		var artist = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };

		artist.SetExternalIds([new SubsonicId("art-tool")]);

		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], artist.ExternalIds);
	}

	[Fact]
	public void RemoveExternalIds_drops_the_ids_from_the_given_provider()
	{
		var artist = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool"), new SubsonicId("art-tool-dupe")], Name = "Tool" };

		artist.RemoveExternalIds(Provider.Subsonic);

		Assert.Equal([new MusicBrainzArtistId("mb-tool")], artist.ExternalIds);
	}

	[Fact]
	public void ToString_is_the_name_and_id()
	{
		var artist = new Artist { Name = "Tool" };

		Assert.Equal($"Tool ({artist.Id})", artist.ToString());
	}
}
