using DiscoTrack.Core.Artists;

namespace DiscoTrack.Core.Tests.Artists;

public class ArtistIdTests
{
	[Fact]
	public void New_ids_are_unique_and_non_empty()
	{
		var ids = Enumerable.Range(0, 100).Select(_ => ArtistId.New()).ToList();

		Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id.Value));
		Assert.Equal(100, ids.Distinct().Count());
	}

	[Fact]
	public void An_artist_gets_an_id_when_constructed()
	{
		var artist = new Artist { Name = "Tool" };

		Assert.NotEqual(Guid.Empty, artist.Id.Value);
	}

	[Fact]
	public void Ids_with_the_same_value_are_equal()
	{
		var value = Guid.NewGuid();

		Assert.Equal(new ArtistId(value), new ArtistId(value));
	}
}
