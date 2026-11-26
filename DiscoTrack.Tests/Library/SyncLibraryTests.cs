using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.ExternalIds;
using DiscoTrack.Library;
using DiscoTrack.Library.SyncLibrary;
using DiscoTrack.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static DiscoTrack.Tests.Library.LibraryValues;

namespace DiscoTrack.Tests.Library;

public class SyncLibraryTests
{
	private static readonly LibraryArtist Tool = new(new SubsonicId("art-tool"), "Tool", new MusicBrainzArtistId("mb-tool"));
	private static readonly LibraryArtist Shihad = new(new SubsonicId("art-shihad"), "Shihad", new MusicBrainzArtistId("mb-shihad"));
	private static readonly LibraryArtist Puscifer = new(new SubsonicId("art-puscifer"), "Puscifer");
	private static readonly LibraryArtist CarinaRound = new(new SubsonicId("art-carina"), "Carina Round");
	private static readonly LibraryArtist Pendulum = new(new SubsonicId("art-pendulum"), "Pendulum");

	private readonly FakeCatalogue _catalogue = new();
	private readonly FakeLibrarySource _source = new();

	private SyncLibrary Sync()
	{
		return new SyncLibrary(_catalogue, _source, NullLogger<SyncLibrary>.Instance);
	}

	[Fact]
	public async Task Empty_library_into_empty_catalogue_adds_nothing()
	{
		await Sync().Run(CancellationToken.None);

		Assert.Empty(_catalogue.Artists);
		Assert.Empty(_catalogue.Albums);
	}

	[Fact]
	public async Task Adds_every_album_and_each_distinct_artist_once()
	{
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-alice", "Alice by the Moon", 2008, TheChecks),
			Album("alb-odelay", "Odelay", 1996, Beck),
			Album("alb-capitale", "Capitale", 2021, McBaise),
		]);

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("art-checks"), new SubsonicId("art-beck"), new SubsonicId("art-mcbaise")], _catalogue.Artists.SelectMany(a => a.ExternalIds));
		Assert.Equal([new SubsonicId("alb-hunting-whales"), new SubsonicId("alb-alice"), new SubsonicId("alb-odelay"), new SubsonicId("alb-capitale")], _catalogue.Albums.SelectMany(a => a.ExternalIds));
	}

	[Fact]
	public async Task Imports_a_new_album_as_tracked()
	{
		_source.Albums.Add(Album("alb-odelay", "Odelay", 1996, Beck));

		await Sync().Run(CancellationToken.None);

		Assert.True(Assert.Single(_catalogue.Albums).Tracked);
	}

	[Fact]
	public async Task Imports_a_new_artist_as_tracked()
	{
		_source.Albums.Add(Album("alb-odelay", "Odelay", 1996, Beck));

		await Sync().Run(CancellationToken.None);

		Assert.True(Assert.Single(_catalogue.Artists).Tracked);
	}

	[Fact]
	public async Task Tracks_and_saves_an_untracked_catalogue_artist_the_library_now_holds()
	{
		var tool = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		_catalogue.Artists.Add(tool);
		_source.Albums.Add(Album("alb-lateralus", "Lateralus", 2001, Tool));

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Same(tool, Assert.Single(_catalogue.Artists));
		Assert.True(tool.Tracked);
		Assert.Contains(tool, response.Artists);
	}

	[Fact]
	public async Task Maps_album_fields_and_one_based_credit_positions()
	{
		_source.Albums.Add(Album("alb-split", "Split", 2010, TheChecks, Beck));

		await Sync().Run(CancellationToken.None);

		var album = Assert.Single(_catalogue.Albums);
		Assert.Equal([new SubsonicId("alb-split")], album.ExternalIds);
		Assert.Equal("Split", album.Title);
		Assert.Equal(2010, album.Year);
		Assert.False(album.IsCompilation);
		Assert.Equal([1, 2], album.Artists.Select(a => a.Position));
		Assert.Equal(["The Checks", "Beck"], album.Artists.Select(a => a.Artist.Name));
	}

	[Fact]
	public async Task Credits_point_at_the_artists_that_were_added()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Sync().Run(CancellationToken.None);

		var addedArtist = Assert.Single(_catalogue.Artists);
		var credit = Assert.Single(Assert.Single(_catalogue.Albums).Artists);
		Assert.Same(addedArtist, credit.Artist);
	}

	[Fact]
	public async Task Credits_reuse_an_artist_already_in_the_catalogue()
	{
		var existingChecks = new Artist { ExternalIds = [new SubsonicId("art-checks")], Name = "The Checks" };
		_catalogue.Artists.Add(existingChecks);
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Sync().Run(CancellationToken.None);

		Assert.Equal([existingChecks.Id], _catalogue.Artists.Select(a => a.Id));
		var credit = Assert.Single(Assert.Single(_catalogue.Albums).Artists);
		Assert.Same(existingChecks, credit.Artist);
	}

	[Fact]
	public async Task Skips_albums_already_in_the_catalogue()
	{
		_catalogue.Artists.Add(new Artist { ExternalIds = [new SubsonicId("art-checks")], Name = "The Checks" });
		_catalogue.Albums.Add(new Album { ExternalIds = [new SubsonicId("alb-hunting-whales")], Title = "Hunting Whales", Year = 2007 });
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-alice", "Alice by the Moon", 2008, TheChecks),
		]);

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("alb-hunting-whales"), new SubsonicId("alb-alice")], _catalogue.Albums.SelectMany(a => a.ExternalIds));
		Assert.Single(_catalogue.Artists);
	}

	[Fact]
	public async Task A_second_run_over_the_same_library_adds_nothing()
	{
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-split", "Split", 2010, TheChecks, Beck),
		]);
		await Sync().Run(CancellationToken.None);
		var artistCount = _catalogue.Artists.Count;
		var albumCount = _catalogue.Albums.Count;

		await Sync().Run(CancellationToken.None);

		Assert.Equal(artistCount, _catalogue.Artists.Count);
		Assert.Equal(albumCount, _catalogue.Albums.Count);
	}

	[Fact]
	public async Task Skips_an_album_with_no_year_and_keeps_the_rest()
	{
		_source.Albums.AddRange([
			Album("alb-untagged", "Untagged", null, TheChecks),
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
		]);

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("alb-hunting-whales")], _catalogue.Albums.SelectMany(a => a.ExternalIds));
		Assert.Single(_catalogue.Artists);
	}

	[Fact]
	public async Task Stores_musicbrainz_ids_alongside_the_source_ids()
	{
		var tool = new LibraryArtist(new SubsonicId("art-tool"), "Tool", new MusicBrainzArtistId("mb-tool"));
		_source.Albums.Add(Album("alb-lateralus", "Lateralus", 2001, tool) with { MusicBrainzId = new MusicBrainzReleaseId("mb-lateralus") });

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Assert.Single(_catalogue.Artists).ExternalIds);
		Assert.Equal([new SubsonicId("alb-lateralus"), new MusicBrainzReleaseId("mb-lateralus")], Assert.Single(_catalogue.Albums).ExternalIds);
	}

	[Fact]
	public async Task Artists_sharing_a_musicbrainz_id_in_the_run_become_one_artist_credited_on_each_album()
	{
		var wolfAlice = new LibraryArtist(new SubsonicId("art-wolf-alice"), "Wolf Alice", new MusicBrainzArtistId("mb-wolf-alice"));
		var wolfAlce = new LibraryArtist(new SubsonicId("art-wolf-alce"), "Wolf Alce", new MusicBrainzArtistId("mb-wolf-alice"));
		_source.Albums.AddRange([
			Album("alb-my-love-is-cool", "My Love Is Cool", 2015, wolfAlice),
			Album("alb-visions", "Visions of a Life", 2017, wolfAlce),
		]);

		await Sync().Run(CancellationToken.None);

		var artist = Assert.Single(_catalogue.Artists);
		Assert.Equal("Wolf Alice", artist.Name);
		Assert.Equal([new SubsonicId("art-wolf-alice"), new MusicBrainzArtistId("mb-wolf-alice"), new SubsonicId("art-wolf-alce")], artist.ExternalIds);
		Assert.Equal(["My Love Is Cool", "Visions of a Life"], _catalogue.Albums.Select(a => a.Title));
		Assert.All(_catalogue.Albums, album => Assert.Same(artist, Assert.Single(album.Artists).Artist));
	}

	[Fact]
	public async Task A_second_run_over_artists_and_albums_sharing_a_musicbrainz_id_saves_nothing()
	{
		var wolfAlice = new LibraryArtist(new SubsonicId("art-wolf-alice"), "Wolf Alice", new MusicBrainzArtistId("mb-wolf-alice"));
		var wolfAlce = new LibraryArtist(new SubsonicId("art-wolf-alce"), "Wolf Alce", new MusicBrainzArtistId("mb-wolf-alice"));
		_source.Albums.AddRange([
			Album("alb-visions", "Visions of a Life", 2017, wolfAlice) with { MusicBrainzId = new MusicBrainzReleaseId("mb-visions") },
			Album("alb-visions-rip", "Visions of a Life", 2017, wolfAlce) with { MusicBrainzId = new MusicBrainzReleaseId("mb-visions") },
		]);
		await Sync().Run(CancellationToken.None);
		_catalogue.Calls.Clear();

		var result = await Sync().Run(CancellationToken.None);

		Assert.Empty(result.GetRight.Artists);
		Assert.Empty(result.GetRight.Albums);
		Assert.Equal([nameof(FakeCatalogue.GetArtists), nameof(FakeCatalogue.GetAlbums), nameof(FakeCatalogue.GetAlbumTracks)], _catalogue.Calls);
		Assert.Equal(
			[new SubsonicId("art-wolf-alice"), new MusicBrainzArtistId("mb-wolf-alice"), new SubsonicId("art-wolf-alce")],
			Assert.Single(_catalogue.Artists).ExternalIds);
		Assert.Equal(
			[new SubsonicId("alb-visions"), new MusicBrainzReleaseId("mb-visions"), new SubsonicId("alb-visions-rip")],
			Assert.Single(_catalogue.Albums).ExternalIds);
	}

	[Fact]
	public async Task Adds_a_duplicate_library_id_to_the_catalogue_entity_without_dropping_the_one_it_has()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		var lateralus = new Album { ExternalIds = [new SubsonicId("alb-lateralus"), new MusicBrainzReleaseId("mb-lateralus")], Title = "Lateralus", Year = 2001 };
		_catalogue.Artists.Add(tool);
		_catalogue.Albums.Add(lateralus);
		var toolAgain = Tool with { Id = new SubsonicId("art-tool-again") };
		_source.Albums.AddRange([
			Album("alb-lateralus", "Lateralus", 2001, Tool) with { MusicBrainzId = new MusicBrainzReleaseId("mb-lateralus") },
			Album("alb-lateralus-rip", "Lateralus", 2001, toolAgain) with { MusicBrainzId = new MusicBrainzReleaseId("mb-lateralus") },
		]);

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal([tool], result.GetRight.Artists);
		Assert.Equal([lateralus], result.GetRight.Albums);
		Assert.Equal([new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool"), new SubsonicId("art-tool-again")], tool.ExternalIds);
		Assert.Equal([new SubsonicId("alb-lateralus"), new MusicBrainzReleaseId("mb-lateralus"), new SubsonicId("alb-lateralus-rip")], lateralus.ExternalIds);
	}

	[Fact]
	public async Task Saves_only_the_catalogue_entities_whose_ids_changed()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool", Tracked = true };
		var shihad = new Artist { ExternalIds = [new SubsonicId("art-shihad-old"), new MusicBrainzArtistId("mb-shihad")], Name = "Shihad", Tracked = true };
		_catalogue.Artists.AddRange([tool, shihad]);
		_catalogue.Albums.Add(new Album { ExternalIds = [new SubsonicId("alb-lateralus")], Title = "Lateralus", Year = 2001 });
		_catalogue.Albums.Add(new Album { ExternalIds = [new SubsonicId("alb-killjoy")], Title = "Killjoy", Year = 1995 });
		_source.Albums.AddRange([
			Album("alb-lateralus", "Lateralus", 2001, Tool),
			Album("alb-killjoy", "Killjoy", 1995, Shihad),
		]);

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal([shihad], result.GetRight.Artists);
		Assert.Empty(result.GetRight.Albums);
		Assert.Equal([new MusicBrainzArtistId("mb-shihad"), new SubsonicId("art-shihad")], shihad.ExternalIds);
		Assert.Single(_catalogue.Calls, call => call == nameof(FakeCatalogue.SaveArtist));
		Assert.DoesNotContain(nameof(FakeCatalogue.SaveAlbum), _catalogue.Calls);
	}

	[Fact]
	public async Task Links_a_library_album_to_the_catalogue_album_holding_its_musicbrainz_id()
	{
		var odelay = new Album { ExternalIds = [new SubsonicId("alb-odelay"), new MusicBrainzReleaseId("mb-odelay")], Title = "Odelay", Year = 1996 };
		_catalogue.Albums.Add(odelay);
		_source.Albums.Add(Album("alb-odelay-rip", "Odelay", 1996, Beck) with { MusicBrainzId = new MusicBrainzReleaseId("mb-odelay") });

		await Sync().Run(CancellationToken.None);

		Assert.Same(odelay, Assert.Single(_catalogue.Albums));
		Assert.Equal([new MusicBrainzReleaseId("mb-odelay"), new SubsonicId("alb-odelay-rip")], odelay.ExternalIds);
	}

	[Fact]
	public async Task Removes_the_library_ids_of_artists_and_albums_no_longer_in_the_library_and_keeps_them_in_the_catalogue()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		var lateralus = new Album { ExternalIds = [new SubsonicId("alb-lateralus"), new MusicBrainzReleaseId("mb-lateralus")], Title = "Lateralus", Year = 2001 };
		var checks = new Artist { ExternalIds = [new SubsonicId("art-checks")], Name = "The Checks", Tracked = true };
		var huntingWhales = new Album { ExternalIds = [new SubsonicId("alb-hunting-whales")], Title = "Hunting Whales", Year = 2007 };
		_catalogue.Artists.AddRange([tool, checks]);
		_catalogue.Albums.AddRange([lateralus, huntingWhales]);
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal([tool], result.GetRight.Artists);
		Assert.Equal([lateralus], result.GetRight.Albums);
		Assert.Equal([new MusicBrainzArtistId("mb-tool")], tool.ExternalIds);
		Assert.Equal([new MusicBrainzReleaseId("mb-lateralus")], lateralus.ExternalIds);
		Assert.Equal([tool, checks], _catalogue.Artists);
		Assert.Equal([lateralus, huntingWhales], _catalogue.Albums);
	}

	[Fact]
	public async Task Leaves_catalogue_entities_that_hold_no_library_id_alone()
	{
		_catalogue.Artists.Add(new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool" });
		_catalogue.Albums.Add(new Album { ExternalIds = [new MusicBrainzReleaseId("mb-lateralus")], Title = "Lateralus", Year = 2001 });
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal(["The Checks"], result.GetRight.Artists.Select(a => a.Name));
		Assert.Equal(["Hunting Whales"], result.GetRight.Albums.Select(a => a.Title));
	}

	[Fact]
	public async Task A_second_run_after_removing_library_ids_saves_nothing()
	{
		_catalogue.Artists.Add(new Artist { ExternalIds = [new SubsonicId("art-tool")], Name = "Tool" });
		_catalogue.Albums.Add(new Album { ExternalIds = [new SubsonicId("alb-lateralus")], Title = "Lateralus", Year = 2001 });
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		await Sync().Run(CancellationToken.None);
		_catalogue.Calls.Clear();

		await Sync().Run(CancellationToken.None);

		Assert.Equal([nameof(FakeCatalogue.GetArtists), nameof(FakeCatalogue.GetAlbums), nameof(FakeCatalogue.GetAlbumTracks)], _catalogue.Calls);
	}

	[Fact]
	public async Task An_empty_library_removes_every_library_id()
	{
		var tool = new Artist { ExternalIds = [new SubsonicId("art-tool"), new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		var lateralus = new Album { ExternalIds = [new SubsonicId("alb-lateralus")], Title = "Lateralus", Year = 2001 };
		_catalogue.Artists.Add(tool);
		_catalogue.Albums.Add(lateralus);

		var result = await Sync().Run(CancellationToken.None);

		Assert.Equal([tool], result.GetRight.Artists);
		Assert.Equal([lateralus], result.GetRight.Albums);
		Assert.Equal([new MusicBrainzArtistId("mb-tool")], tool.ExternalIds);
		Assert.Empty(lateralus.ExternalIds);
	}

	[Fact]
	public async Task Saves_artists_before_albums()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Sync().Run(CancellationToken.None);

		Assert.Equal(
			[nameof(FakeCatalogue.GetArtists), nameof(FakeCatalogue.GetAlbums), nameof(FakeCatalogue.SaveArtist), nameof(FakeCatalogue.SaveAlbum), nameof(FakeCatalogue.GetAlbumTracks)],
			_catalogue.Calls);
	}

	[Fact]
	public async Task Imports_the_tracks_of_a_new_album_with_their_fields_ids_and_credits()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.AddRange([
			Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer, CarinaRound) with { DurationSeconds = 268, MusicBrainzId = new MusicBrainzRecordingId("rec-tiny") },
			Track("song-toma", "alb-parole", 1, 5, "Toma", Puscifer),
		]);

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		var album = Assert.Single(_catalogue.Albums);
		var tracks = _catalogue.Tracks[album.Id];
		Assert.Equal(["Tiny Monsters", "Toma"], tracks.Select(track => track.Title));
		Assert.Equal(tracks, response.Tracks);
		var tiny = tracks[0];
		Assert.Equal(album.Id, tiny.AlbumId);
		Assert.Equal(1, tiny.DiscNumber);
		Assert.Equal(3, tiny.Position);
		Assert.Equal(268000, tiny.DurationMs);
		Assert.Equal([new SubsonicId("song-tiny"), new MusicBrainzRecordingId("rec-tiny")], tiny.ExternalIds);
		Assert.Equal(["Puscifer", "Carina Round"], tiny.Artists.Select(credit => credit.Artist.Name));
		Assert.Equal([1, 2], tiny.Artists.Select(credit => credit.Position));
		Assert.Null(tracks[1].DurationMs);
	}

	[Fact]
	public async Task Credits_a_track_on_the_catalogue_artists_and_credits_an_artist_listed_twice_once()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer, Puscifer));

		await Sync().Run(CancellationToken.None);

		var credit = Assert.Single(Assert.Single(_catalogue.Tracks.Values.Single()).Artists);
		Assert.Same(Assert.Single(_catalogue.Artists), credit.Artist);
	}

	[Fact]
	public async Task Adds_an_artist_credited_only_on_tracks_as_untracked()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer, CarinaRound));

		await Sync().Run(CancellationToken.None);

		Assert.Equal([("Puscifer", true), ("Carina Round", false)], _catalogue.Artists.Select(artist => (artist.Name, artist.Tracked)));
		Assert.Equal([new SubsonicId("art-carina")], _catalogue.Artists[1].ExternalIds);
	}

	[Fact]
	public async Task Leaves_an_untracked_catalogue_artist_untracked_when_the_library_only_credits_it_on_tracks()
	{
		var carina = new Artist { ExternalIds = [new SubsonicId("art-carina")], Name = "Carina Round" };
		_catalogue.Artists.Add(carina);
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer, CarinaRound));

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.False(carina.Tracked);
		Assert.Equal([new SubsonicId("art-carina")], carina.ExternalIds);
		Assert.DoesNotContain(carina, response.Artists);
	}

	[Fact]
	public async Task A_second_run_over_the_same_tracks_saves_no_tracks()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer, CarinaRound));
		await Sync().Run(CancellationToken.None);
		_catalogue.Calls.Clear();

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Empty(response.Tracks);
		Assert.Equal([nameof(FakeCatalogue.GetArtists), nameof(FakeCatalogue.GetAlbums), nameof(FakeCatalogue.GetAlbumTracks)], _catalogue.Calls);
	}

	[Fact]
	public async Task Adds_a_track_that_is_new_to_an_album_already_in_the_catalogue()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer));
		await Sync().Run(CancellationToken.None);
		_source.Tracks.Add(Track("song-toma", "alb-parole", 1, 5, "Toma", Puscifer));

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Equal(["Tiny Monsters", "Toma"], _catalogue.Tracks.Values.Single().Select(track => track.Title));
		Assert.Equal("Toma", Assert.Single(response.Tracks).Title);
	}

	[Fact]
	public async Task Gives_the_library_id_to_a_catalogue_track_holding_the_same_recording_id()
	{
		var (album, missing) = await OwnedAlbumWithTrack(new Track
		{
			AlbumId = default, Title = "Tiny Monsters (album version)", DiscNumber = 1, Position = 9, ExternalIds = [new MusicBrainzRecordingId("rec-tiny")],
		});
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer) with { MusicBrainzId = new MusicBrainzRecordingId("rec-tiny") });

		await Sync().Run(CancellationToken.None);

		var track = Assert.Single(_catalogue.Tracks[album.Id]);
		Assert.Equal(missing.Id, track.Id);
		Assert.Equal([new MusicBrainzRecordingId("rec-tiny"), new SubsonicId("song-tiny")], track.ExternalIds);
		Assert.Equal("Tiny Monsters (album version)", track.Title);
	}

	[Fact]
	public async Task Gives_the_library_id_to_a_catalogue_track_at_the_same_disc_and_position_with_a_matching_title()
	{
		var (album, missing) = await OwnedAlbumWithTrack(new Track
		{
			AlbumId = default, Title = "Tiny Monsters", DiscNumber = 1, Position = 3, ExternalIds = [new MusicBrainzRecordingId("rec-tiny")],
		});
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "tiny monsters!", Puscifer));

		await Sync().Run(CancellationToken.None);

		var track = Assert.Single(_catalogue.Tracks[album.Id]);
		Assert.Equal(missing.Id, track.Id);
		Assert.Equal([new MusicBrainzRecordingId("rec-tiny"), new SubsonicId("song-tiny")], track.ExternalIds);
	}

	[Theory]
	[InlineData(2, 3, "Tiny Monsters")]
	[InlineData(1, 4, "Tiny Monsters")]
	[InlineData(1, 3, "Toma")]
	public async Task Adds_a_track_when_the_disc_position_or_title_differs_from_the_catalogue_track(int discNumber, int trackNumber, string title)
	{
		var (album, _) = await OwnedAlbumWithTrack(new Track { AlbumId = default, Title = "Tiny Monsters", DiscNumber = 1, Position = 3 });
		_source.Tracks.Add(Track("song-other", "alb-parole", discNumber, trackNumber, title, Puscifer));

		await Sync().Run(CancellationToken.None);

		Assert.Equal(2, _catalogue.Tracks[album.Id].Count);
	}

	[Fact]
	public async Task Keeps_two_different_recordings_at_the_same_position_as_separate_tracks()
	{
		var (album, _) = await OwnedAlbumWithTrack(new Track
		{
			AlbumId = default, Title = "Tiny Monsters", DiscNumber = 1, Position = 3, ExternalIds = [new MusicBrainzRecordingId("rec-tiny")],
		});
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer) with { MusicBrainzId = new MusicBrainzRecordingId("rec-tiny-remix") });

		await Sync().Run(CancellationToken.None);

		Assert.Equal(
			[[new MusicBrainzRecordingId("rec-tiny")], [new SubsonicId("song-tiny"), new MusicBrainzRecordingId("rec-tiny-remix")]],
			_catalogue.Tracks[album.Id].Select(track => track.ExternalIds));
	}

	[Fact]
	public async Task One_track_on_two_library_albums_of_the_same_catalogue_album_holds_both_library_ids()
	{
		_source.Albums.AddRange([
			Album("alb-parole", "Conditions of My Parole", 2011, Puscifer) with { MusicBrainzId = new MusicBrainzReleaseId("mb-parole") },
			Album("alb-parole-rip", "Conditions of My Parole", 2011, Puscifer) with { MusicBrainzId = new MusicBrainzReleaseId("mb-parole") },
		]);
		_source.Tracks.AddRange([
			Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer) with { MusicBrainzId = new MusicBrainzRecordingId("rec-tiny") },
			Track("song-tiny-rip", "alb-parole-rip", 1, 1, "Tiny Monsters", Puscifer) with { MusicBrainzId = new MusicBrainzRecordingId("rec-tiny") },
			Track("song-bonus", "alb-parole-rip", 1, 2, "Bonus", Puscifer),
		]);

		await Sync().Run(CancellationToken.None);

		var tracks = _catalogue.Tracks[Assert.Single(_catalogue.Albums).Id];
		Assert.Equal(["Tiny Monsters", "Bonus"], tracks.Select(track => track.Title));
		Assert.Equal([new SubsonicId("song-tiny"), new MusicBrainzRecordingId("rec-tiny"), new SubsonicId("song-tiny-rip")], tracks[0].ExternalIds);
	}

	[Fact]
	public async Task Removes_the_library_id_of_a_track_no_longer_in_the_library_and_keeps_the_track()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.AddRange([
			Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer) with { MusicBrainzId = new MusicBrainzRecordingId("rec-tiny") },
			Track("song-toma", "alb-parole", 1, 5, "Toma", Puscifer),
		]);
		await Sync().Run(CancellationToken.None);
		_source.Tracks.RemoveAt(0);

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		var tracks = _catalogue.Tracks.Values.Single();
		Assert.Equal([[new MusicBrainzRecordingId("rec-tiny")], [new SubsonicId("song-toma")]], tracks.Select(track => track.ExternalIds));
		Assert.Equal("Tiny Monsters", Assert.Single(response.Tracks).Title);
	}

	[Fact]
	public async Task Replaces_the_library_id_of_a_track_whose_file_was_replaced()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer));
		await Sync().Run(CancellationToken.None);
		_source.Tracks[0] = Track("song-tiny-flac", "alb-parole", 1, 3, "Tiny Monsters", Puscifer);

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new SubsonicId("song-tiny-flac")], Assert.Single(_catalogue.Tracks.Values.Single()).ExternalIds);
	}

	[Fact]
	public async Task Removes_the_library_ids_of_the_tracks_of_an_album_no_longer_in_the_library()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer));
		await Sync().Run(CancellationToken.None);
		_source.Albums.Clear();
		_source.Tracks.Clear();

		await Sync().Run(CancellationToken.None);

		var track = Assert.Single(_catalogue.Tracks.Values.Single());
		Assert.Equal("Tiny Monsters", track.Title);
		Assert.Empty(track.ExternalIds);
	}

	[Fact]
	public async Task Does_not_read_the_tracks_of_albums_the_library_never_held()
	{
		_catalogue.Albums.Add(new Album { ExternalIds = [new MusicBrainzReleaseGroupId("rg-undertow")], Title = "Undertow", Year = 1993 });

		await Sync().Run(CancellationToken.None);

		Assert.DoesNotContain(nameof(FakeCatalogue.GetAlbumTracks), _catalogue.Calls);
	}

	[Fact]
	public async Task Imports_no_tracks_for_an_album_it_could_not_import()
	{
		_source.Albums.Add(Album("alb-untagged", "Untagged", null, TheChecks));
		_source.Tracks.Add(Track("song-untagged", "alb-untagged", 1, 1, "Untagged", TheChecks));

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Empty(response.Tracks);
		Assert.Empty(_catalogue.Tracks);
	}

	[Fact]
	public async Task Imports_no_tracks_for_an_album_it_could_not_save()
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer));
		_catalogue.FailingMethod = nameof(FakeCatalogue.SaveAlbum);

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Empty(response.Tracks);
		Assert.DoesNotContain(nameof(FakeCatalogue.GetAlbumTracks), _catalogue.Calls);
	}

	[Theory]
	[InlineData(nameof(FakeCatalogue.GetAlbumTracks))]
	[InlineData(nameof(FakeCatalogue.SaveAlbumTracks))]
	public async Task A_failed_track_read_or_save_is_reported_against_the_album(string failingMethod)
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		_source.Tracks.Add(Track("song-tiny", "alb-parole", 1, 3, "Tiny Monsters", Puscifer));
		_catalogue.FailingMethod = failingMethod;

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Empty(response.Tracks);
		var failure = Assert.Single(response.Failures);
		Assert.Equal(SyncAction.Save, failure.Action);
		Assert.Same(Assert.Single(_catalogue.Albums), failure.Entity);
	}

	[Fact]
	public async Task Returns_a_read_library_error_when_reading_the_tracks_fails_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_source.FailingMethod = nameof(FakeLibrarySource.GetTracks);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToReadLibraryError>(Assert.Single(result.GetLeft));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Returns_a_read_catalogue_error_when_reading_artists_fails_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.GetArtists);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToReadCatalogueError>(Assert.Single(result.GetLeft));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Returns_a_read_catalogue_error_when_reading_albums_fails_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_catalogue.FailingMethod = nameof(FakeCatalogue.GetAlbums);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToReadCatalogueError>(Assert.Single(result.GetLeft));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Returns_a_read_library_error_when_reading_the_library_fails_and_writes_nothing()
	{
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));
		_source.FailingMethod = nameof(FakeLibrarySource.GetAlbums);

		var result = await Sync().Run(CancellationToken.None);

		Assert.IsType<FailedToReadLibraryError>(Assert.Single(result.GetLeft));

		AssertNothingWritten();
	}

	[Fact]
	public async Task A_failed_artist_save_is_reported_and_the_other_artists_are_still_saved()
	{
		var catalogue = new Mock<ICatalogue>();
		catalogue.Setup(c => c.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue
			.Setup(c => c.SaveArtist(It.Is<Artist>(artist => artist.Name == "The Checks"), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("database is locked"));
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-odelay", "Odelay", 1996, Beck),
		]);

		var result = await new SyncLibrary(catalogue.Object, _source, NullLogger<SyncLibrary>.Instance).Run(CancellationToken.None);

		var response = result.GetRight;
		var failure = Assert.Single(response.Failures);
		Assert.Equal(SyncAction.Save, failure.Action);
		Assert.Equal("The Checks", Assert.IsType<Artist>(failure.Entity).Name);
		Assert.Equal("database is locked", failure.Reason);
		Assert.Equal(["Beck"], response.Artists.Select(a => a.Name));
	}

	[Fact]
	public async Task A_failed_album_save_is_reported_and_the_other_albums_are_still_saved()
	{
		var catalogue = new Mock<ICatalogue>();
		catalogue.Setup(c => c.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue
			.Setup(c => c.SaveAlbum(It.Is<Album>(album => album.Title == "Hunting Whales"), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("database is locked"));
		_source.Albums.AddRange([
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
			Album("alb-odelay", "Odelay", 1996, Beck),
		]);

		var result = await new SyncLibrary(catalogue.Object, _source, NullLogger<SyncLibrary>.Instance).Run(CancellationToken.None);

		var response = result.GetRight;
		var failure = Assert.Single(response.Failures);
		Assert.Equal(SyncAction.Save, failure.Action);
		Assert.Equal("Hunting Whales", Assert.IsType<Album>(failure.Entity).Title);
		Assert.Equal("database is locked", failure.Reason);
		Assert.Equal(["Odelay"], response.Albums.Select(a => a.Title));
		Assert.Equal(["The Checks", "Beck"], response.Artists.Select(a => a.Name));
	}

	[Fact]
	public async Task Cancellation_during_a_save_propagates_instead_of_becoming_a_failure()
	{
		var catalogue = new Mock<ICatalogue>();
		catalogue.Setup(c => c.GetArtists(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbums(It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue.Setup(c => c.GetAlbumTracks(It.IsAny<AlbumId>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		catalogue
			.Setup(c => c.SaveArtist(It.IsAny<Artist>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());
		_source.Albums.Add(Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks));

		await Assert.ThrowsAsync<OperationCanceledException>(() => new SyncLibrary(catalogue.Object, _source, NullLogger<SyncLibrary>.Instance).Run(CancellationToken.None));
	}

	[Fact]
	public async Task Saves_and_returns_the_artists_and_albums_it_linked()
	{
		var tool = new Artist { ExternalIds = [new MusicBrainzArtistId("mb-tool")], Name = "Tool" };
		var lateralus = new Album { ExternalIds = [new MusicBrainzReleaseId("mb-lateralus")], Title = "Lateralus", Year = 2001 };
		_catalogue.Artists.Add(tool);
		_catalogue.Albums.Add(lateralus);
		_source.Albums.Add(Album("alb-lateralus", "Lateralus", 2001, Tool) with { MusicBrainzId = new MusicBrainzReleaseId("mb-lateralus") });

		var result = await Sync().Run(CancellationToken.None);

		var response = result.GetRight;
		Assert.Equal([tool], response.Artists);
		Assert.Equal([lateralus], response.Albums);
		Assert.Equal([new MusicBrainzArtistId("mb-tool"), new SubsonicId("art-tool")], tool.ExternalIds);
		Assert.Equal([new MusicBrainzReleaseId("mb-lateralus"), new SubsonicId("alb-lateralus")], lateralus.ExternalIds);
		Assert.Empty(response.Failures);
		Assert.Contains(nameof(FakeCatalogue.SaveArtist), _catalogue.Calls);
		Assert.Contains(nameof(FakeCatalogue.SaveAlbum), _catalogue.Calls);
	}

	[Fact]
	public async Task Saves_an_artist_once_when_it_is_added_and_linked_in_the_same_run()
	{
		var toolAgain = Tool with { Id = new SubsonicId("art-tool-again") };
		_source.Albums.AddRange([
			Album("alb-lateralus", "Lateralus", 2001, Tool),
			Album("alb-aenima", "Aenima", 1996, toolAgain),
		]);

		var result = await Sync().Run(CancellationToken.None);

		Assert.Single(result.GetRight.Artists);
		Assert.Single(_catalogue.Calls, call => call == nameof(FakeCatalogue.SaveArtist));
	}

	[Fact]
	public async Task Returns_the_added_artists_and_albums_and_the_albums_it_could_not_import()
	{
		_source.Albums.AddRange([
			Album("alb-untagged", "Untagged", null, TheChecks),
			Album("alb-hunting-whales", "Hunting Whales", 2007, TheChecks),
		]);

		var result = await Sync().Run(CancellationToken.None);

		var response = result.GetRight;
		Assert.Equal([new SubsonicId("art-checks")], response.Artists.SelectMany(a => a.ExternalIds));
		Assert.Equal([new SubsonicId("alb-hunting-whales")], response.Albums.SelectMany(a => a.ExternalIds));
		var failure = Assert.Single(response.Failures);
		Assert.Equal(SyncAction.Import, failure.Action);
		Assert.Equal(new SubsonicId("alb-untagged"), Assert.IsType<LibraryAlbum>(failure.Entity).Id);
	}

	[Fact]
	public async Task Cancellation_propagates_instead_of_becoming_an_error()
	{
		var source = new Mock<ILibrarySource>();
		source
			.Setup(s => s.GetAlbums(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => new SyncLibrary(_catalogue, source.Object, NullLogger<SyncLibrary>.Instance).Run(CancellationToken.None));

		AssertNothingWritten();
	}

	[Fact]
	public async Task Gives_the_library_id_to_a_namesake_the_catalogue_album_credits_and_tracks_it()
	{
		var (house, drumAndBass) = TwoPendulums();

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Equal([new MusicBrainzArtistId("mb-pendulum-dnb"), new SubsonicId("art-pendulum")], drumAndBass.ExternalIds);
		Assert.True(drumAndBass.Tracked);
		Assert.Equal([new SubsonicId("art-pendulum"), new MusicBrainzArtistId("mb-pendulum-house")], house.ExternalIds);
		Assert.Equal([drumAndBass], response.Artists);
		Assert.Equal(2, _catalogue.Artists.Count);
	}

	[Fact]
	public async Task A_second_run_over_artists_sharing_a_library_id_saves_nothing()
	{
		TwoPendulums();
		await Sync().Run(CancellationToken.None);
		_catalogue.Calls.Clear();

		var response = (await Sync().Run(CancellationToken.None)).GetRight;

		Assert.Empty(response.Artists);
		Assert.Empty(response.Albums);
		Assert.DoesNotContain(_catalogue.Calls, call => call.StartsWith("Save"));
	}

	[Fact]
	public async Task Does_not_give_the_library_id_to_a_credited_artist_with_another_name()
	{
		var (_, drumAndBass) = TwoPendulums();
		drumAndBass.Name = "Pendulum UK";

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new MusicBrainzArtistId("mb-pendulum-dnb")], drumAndBass.ExternalIds);
		Assert.False(drumAndBass.Tracked);
	}

	[Fact]
	public async Task Every_artist_sharing_a_library_id_loses_it_when_it_leaves_the_library()
	{
		var (house, drumAndBass) = TwoPendulums();
		await Sync().Run(CancellationToken.None);
		_source.Albums.Clear();

		await Sync().Run(CancellationToken.None);

		Assert.Equal([new MusicBrainzArtistId("mb-pendulum-house")], house.ExternalIds);
		Assert.Equal([new MusicBrainzArtistId("mb-pendulum-dnb")], drumAndBass.ExternalIds);
	}

	[Fact]
	public async Task Credits_a_new_album_on_the_artist_that_has_held_the_shared_library_id_longest()
	{
		var (house, _) = TwoPendulums();
		await Sync().Run(CancellationToken.None);
		_source.Albums.Add(Album("alb-elemental", "Elemental", 2021, Pendulum));

		await Sync().Run(CancellationToken.None);

		var elemental = _catalogue.Albums.Single(album => album.Title == "Elemental");
		Assert.Same(house, Assert.Single(elemental.Artists).Artist);
	}

	[Fact]
	public async Task Credits_a_track_on_the_artist_its_album_credits_when_several_share_the_library_id()
	{
		var (house, drumAndBass) = TwoPendulums();
		_source.Tracks.AddRange([
			Track("song-coma", "alb-knocks", 1, 1, "Coma", Pendulum),
			Track("song-genesis", "alb-immersion", 1, 1, "Genesis", Pendulum),
		]);

		await Sync().Run(CancellationToken.None);

		var tracks = _catalogue.Tracks.Values.SelectMany(albumTracks => albumTracks).ToDictionary(track => track.Title);
		Assert.Same(house, Assert.Single(tracks["Coma"].Artists).Artist);
		Assert.Same(drumAndBass, Assert.Single(tracks["Genesis"].Artists).Artist);
	}

	private (Artist House, Artist DrumAndBass) TwoPendulums()
	{
		var house = new Artist
		{
			Id = new ArtistId(Guid.Parse("00000000-0000-7000-8000-000000000001")),
			ExternalIds = [new SubsonicId("art-pendulum"), new MusicBrainzArtistId("mb-pendulum-house")],
			Name = "Pendulum",
			Tracked = true,
		};
		var drumAndBass = new Artist
		{
			Id = new ArtistId(Guid.Parse("00000000-0000-7000-8000-000000000002")),
			ExternalIds = [new MusicBrainzArtistId("mb-pendulum-dnb")],
			Name = "Pendulum",
		};
		_catalogue.Artists.AddRange([house, drumAndBass]);
		_catalogue.Albums.AddRange([
			new Album { ExternalIds = [new SubsonicId("alb-knocks")], Title = "3 Knocks", Year = 1997, Tracked = true, Artists = [new AlbumArtist { Artist = house, Position = 1 }] },
			new Album { ExternalIds = [new SubsonicId("alb-immersion")], Title = "Immersion", Year = 2010, Tracked = true, Artists = [new AlbumArtist { Artist = drumAndBass, Position = 1 }] },
		]);
		_source.Albums.AddRange([
			Album("alb-knocks", "3 Knocks", 1997, Pendulum),
			Album("alb-immersion", "Immersion", 2010, Pendulum),
		]);

		return (house, drumAndBass);
	}

	private async Task<(Album Album, Track Track)> OwnedAlbumWithTrack(Track template)
	{
		_source.Albums.Add(Album("alb-parole", "Conditions of My Parole", 2011, Puscifer));
		await Sync().Run(CancellationToken.None);
		var album = Assert.Single(_catalogue.Albums);
		var track = new Track
		{
			AlbumId = album.Id,
			Title = template.Title,
			DiscNumber = template.DiscNumber,
			Position = template.Position,
			ExternalIds = template.ExternalIds,
		};
		_catalogue.Tracks[album.Id] = [track];

		return (album, track);
	}

	private void AssertNothingWritten()
	{
		Assert.DoesNotContain(nameof(FakeCatalogue.SaveAlbumTracks), _catalogue.Calls);
		Assert.DoesNotContain(nameof(FakeCatalogue.SaveArtist), _catalogue.Calls);
		Assert.DoesNotContain(nameof(FakeCatalogue.SaveAlbum), _catalogue.Calls);
		Assert.Empty(_catalogue.Artists);
		Assert.Empty(_catalogue.Albums);
	}
}
