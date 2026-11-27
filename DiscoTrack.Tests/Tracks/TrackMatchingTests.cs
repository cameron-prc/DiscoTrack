using DiscoTrack.Albums;
using DiscoTrack.ExternalIds;
using DiscoTrack.Tracks;

namespace DiscoTrack.Tests.Tracks;

public class TrackMatchingTests
{
	private static readonly AlbumId Album = AlbumId.New();
	private static readonly AlbumId Absorbed = AlbumId.New();

	[Fact]
	public void FindMatch_prefers_a_track_sharing_an_external_id_over_one_at_the_same_place()
	{
		var samePlace = Track(Album, 1, 1, "The Grudge");
		var sameRecording = Track(Album, 1, 9, "The Grudge (live)", new MusicBrainzRecordingId("rec-grudge"));

		var match = new[] { samePlace, sameRecording }.FindMatch([new SubsonicId("song-grudge"), new MusicBrainzRecordingId("rec-grudge")], 1, 1, "The Grudge");

		Assert.Same(sameRecording, match);
	}

	[Fact]
	public void FindMatch_matches_a_track_sharing_an_isrc_wherever_it_sits()
	{
		var samePlace = Track(Album, 1, 1, "The Grudge");
		var sameIsrc = Track(Album, 1, 9, "The Grudge (live)", new Isrc("USVR10100101"));

		var match = new[] { samePlace, sameIsrc }.FindMatch([new SubsonicId("song-grudge"), new Isrc("usvr10100101")], 1, 1, "The Grudge");

		Assert.Same(sameIsrc, match);
	}

	[Fact]
	public void FindMatch_prefers_a_track_sharing_a_providers_own_id_over_one_sharing_an_isrc()
	{
		var sameIsrc = Track(Album, 1, 1, "The Grudge", new Isrc("USVR10100101"));
		var sameRecording = Track(Album, 1, 9, "The Grudge (live)", new MusicBrainzRecordingId("rec-grudge"));

		var match = new[] { sameIsrc, sameRecording }.FindMatch([new Isrc("USVR10100101"), new MusicBrainzRecordingId("rec-grudge")], 1, 1, "The Grudge");

		Assert.Same(sameRecording, match);
	}

	[Fact]
	public void An_isrc_is_the_same_in_any_letter_case()
	{
		Assert.Equal(new Isrc("USVR10100101"), new Isrc("usvr10100101"));
		Assert.Equal("USVR10100101", new Isrc("usvr10100101").Value);
	}

	[Fact]
	public void FindMatch_matches_the_same_disc_and_position_when_the_titles_differ_only_in_case_and_punctuation()
	{
		var track = Track(Album, 1, 1, "The Grudge", new MusicBrainzRecordingId("rec-grudge"));

		var match = new[] { track }.FindMatch([new SubsonicId("song-grudge")], 1, 1, "the grudge!");

		Assert.Same(track, match);
	}

	[Theory]
	[InlineData(2, 1, "The Grudge")]
	[InlineData(1, 2, "The Grudge")]
	[InlineData(1, 1, "Schism")]
	public void FindMatch_finds_nothing_when_the_disc_position_or_title_differs(int discNumber, int position, string title)
	{
		var track = Track(Album, 1, 1, "The Grudge");

		Assert.Null(new[] { track }.FindMatch([new SubsonicId("song-grudge")], discNumber, position, title));
	}

	[Fact]
	public void FindMatch_does_not_match_by_place_when_both_hold_a_recording_id()
	{
		var track = Track(Album, 1, 1, "The Grudge", new MusicBrainzRecordingId("rec-grudge"));

		Assert.Null(new[] { track }.FindMatch([new MusicBrainzRecordingId("rec-grudge-remix")], 1, 1, "The Grudge"));
	}

	[Fact]
	public void MergeInto_gives_a_track_on_both_albums_the_ids_of_both_and_keeps_the_survivors_track()
	{
		var owned = Track(Album, 1, 1, "The Grudge", new SubsonicId("song-grudge"), new MusicBrainzRecordingId("rec-grudge"));
		var otherEdition = Track(Absorbed, 1, 2, "The Grudge", new SubsonicId("song-grudge-vinyl"), new MusicBrainzRecordingId("rec-grudge"));

		var merged = new[] { otherEdition }.MergeInto(Album, [owned]);

		var track = Assert.Single(merged);
		Assert.Same(owned, track);
		Assert.Equal([new SubsonicId("song-grudge"), new MusicBrainzRecordingId("rec-grudge"), new SubsonicId("song-grudge-vinyl")], track.ExternalIds);
	}

	[Fact]
	public void MergeInto_moves_a_track_only_the_absorbed_album_has_keeping_its_id_and_fields()
	{
		var owned = Track(Album, 1, 1, "The Grudge");
		var bonus = Track(Absorbed, 2, 1, "Bonus", new SubsonicId("song-bonus"));
		bonus.DurationMs = 1000;

		var merged = new[] { bonus }.MergeInto(Album, [owned]);

		Assert.Equal(2, merged.Count);
		var moved = merged[1];
		Assert.Equal(bonus.Id, moved.Id);
		Assert.Equal(Album, moved.AlbumId);
		Assert.Equal("Bonus", moved.Title);
		Assert.Equal(2, moved.DiscNumber);
		Assert.Equal(1, moved.Position);
		Assert.Equal(1000, moved.DurationMs);
		Assert.Equal([new SubsonicId("song-bonus")], moved.ExternalIds);
	}

	[Fact]
	public void MergeInto_keeps_different_recordings_at_the_same_place_as_separate_tracks()
	{
		var owned = Track(Album, 1, 1, "The Grudge", new MusicBrainzRecordingId("rec-grudge"));
		var remix = Track(Absorbed, 1, 1, "The Grudge", new MusicBrainzRecordingId("rec-grudge-remix"));

		var merged = new[] { remix }.MergeInto(Album, [owned]);

		Assert.Equal(2, merged.Count);
	}

	private static Track Track(AlbumId albumId, int discNumber, int position, string title, params ExternalId[] externalIds)
	{
		return new Track { AlbumId = albumId, DiscNumber = discNumber, Position = position, Title = title, ExternalIds = externalIds };
	}
}
