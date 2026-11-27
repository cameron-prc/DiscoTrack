using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.Discography;
using DiscoTrack.Discography.SyncAlbumWithDiscography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Tracks;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.ListenHistory.ResolvePlays;

public class ResolvePlaysService(
	IDiscographySource source,
	ICatalogue catalogue,
	ISyncAlbumWithDiscographyService syncAlbumWithDiscography,
	ILogger<ResolvePlaysService> logger) : IResolvePlaysService
{
	/// <summary>
	/// <para>Makes the catalogue hold the album, track and artists of each play, carrying the play's provider ids. A play is found again through those ids; nothing is stored on the play.</para>
	/// <para>
	/// An album the catalogue lacks is created untracked from the discography release it resolves to:
	/// first the release the discography links to the provider's album,
	/// second a release of the recording carrying the play's ISRC whose title matches the album's,
	/// third any release of that recording, which is a guess: the play then sits on an album that holds its recording.
	/// </para>
	/// <para>
	/// When the discography answers that it has no such release, the album, its artists and the played track are created untracked
	/// from the play alone, holding only the provider's ids. Nothing is created when the discography could not be asked.
	/// </para>
	/// <para>A play whose track is not on an album that has the discography's tracklist is left unresolved.</para>
	/// </summary>
	public async Task<Either<IList<IResolvePlaysError>, ResolvePlaysResponse>> Run(IReadOnlyList<Play> plays, CancellationToken cancellationToken)
	{
		List<Album> knownAlbums;
		HashSet<ExternalId> artistExternalIds;

		try
		{
			knownAlbums = (await catalogue.GetAlbums(cancellationToken)).ToList();
			artistExternalIds = (await catalogue.GetArtists(cancellationToken)).SelectMany(artist => artist.ExternalIds).ToHashSet();
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return new List<IResolvePlaysError> { new UnableToReadCatalogueError(exception.Message) };
		}

		var resolvedPlays = new List<Play>();
		var createdAlbums = new List<Album>();

		foreach (var albumPlays in plays.Where(play => play.AlbumId is not null).GroupBy(play => play.AlbumId!))
		{
			try
			{
				resolvedPlays.AddRange(await ResolveAlbumPlays(albumPlays.Key, albumPlays.ToList(), knownAlbums, artistExternalIds, createdAlbums, cancellationToken));
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				logger.LogWarning(exception, "Failed to resolve the plays of {AlbumTitle} ({AlbumId})", albumPlays.First().AlbumTitle, albumPlays.Key);
			}
		}

		return new ResolvePlaysResponse(resolvedPlays, plays.Except(resolvedPlays).ToList(), createdAlbums);
	}

	private async Task<IReadOnlyList<Play>> ResolveAlbumPlays(
		ExternalId albumId,
		IReadOnlyList<Play> plays,
		List<Album> knownAlbums,
		HashSet<ExternalId> artistExternalIds,
		List<Album> createdAlbums,
		CancellationToken cancellationToken)
	{
		var album = knownAlbums.FirstOrDefault(knownAlbum => knownAlbum.ExternalIds.Contains(albumId));

		if (album is null)
		{
			var releaseId = await FindRelease(albumId, plays[0], cancellationToken);

			if (releaseId is null)
			{
				logger.LogDebug("The discography has no release for {AlbumTitle} ({AlbumId})", plays[0].AlbumTitle, albumId);

				album = await CreateProviderAlbum(albumId, plays[0], knownAlbums, artistExternalIds, cancellationToken);
				createdAlbums.Add(album);
			}
			else
			{
				var heldAlbum = knownAlbums.FirstOrDefault(knownAlbum => knownAlbum.ExternalIds.Contains(releaseId));

				album = heldAlbum ?? new Album { Title = plays[0].AlbumTitle ?? plays[0].TrackTitle, Year = 0, ExternalIds = [releaseId] };
				album.ExternalIds = album.ExternalIds.Append(albumId).ToList();

				await catalogue.SaveAlbum(album, cancellationToken);
				knownAlbums.Remove(album);

				album = await SyncAlbum(album, knownAlbums, cancellationToken);

				if (album is null)
				{
					return [];
				}

				if (heldAlbum is null)
				{
					createdAlbums.Add(album);
				}
			}
		}

		var tracks = (await catalogue.GetAlbumTracks(album.Id, cancellationToken)).ToList();

		if (tracks.Count == 0 && HoldsDiscographyAlbum(album))
		{
			knownAlbums.Remove(album);
			album = await SyncAlbum(album, knownAlbums, cancellationToken);

			if (album is null)
			{
				return [];
			}

			tracks = (await catalogue.GetAlbumTracks(album.Id, cancellationToken)).ToList();
		}

		// Only an album without the discography's tracklist takes tracks from plays: on a listed album an unmatched play is a matching failure
		var takesPlayedTracks = !tracks.Any(track => track.ExternalIds.Any(externalId => externalId.Provider == source.RecordingProvider));
		var resolvedPlays = new List<Play>();
		var changedArtists = new List<Artist>();
		var tracksChanged = false;
		List<Artist>? knownArtists = null;

		foreach (var play in plays)
		{
			var track = tracks.FirstOrDefault(candidate => candidate.ExternalIds.Contains(play.TrackId)) ?? FindTrack(tracks, play);

			if (track is null && takesPlayedTracks)
			{
				knownArtists ??= (await catalogue.GetArtists(cancellationToken)).ToList();
				track = new Track
				{
					AlbumId = album.Id,
					Title = play.TrackTitle,
					DiscNumber = play.DiscNumber ?? 1,
					Position = play.TrackNumber ?? 0,
					DurationMs = play.DurationMs,
					Artists = ChooseArtists(play, knownArtists, artistExternalIds, changedArtists)
						.Select((artist, index) => new TrackArtist { Artist = artist, Position = index + 1 })
						.ToList(),
				};
				tracks.Add(track);
			}

			if (track is null)
			{
				logger.LogDebug("{Album} has no track matching {Play}", album, play);

				continue;
			}

			var heldIds = track.ExternalIds.Count;

			track.AddExternalIds(PlayIds(play));
			tracksChanged = tracksChanged || track.ExternalIds.Count != heldIds;

			changedArtists.AddRange(IdentifyArtists(play, track, album, artistExternalIds));
			resolvedPlays.Add(play);
		}

		foreach (var artist in changedArtists.Distinct())
		{
			await catalogue.SaveArtist(artist, cancellationToken);
		}

		if (tracksChanged)
		{
			await catalogue.SaveAlbumTracks(album.Id, tracks, cancellationToken);
		}

		return resolvedPlays;
	}

	// The album as the play describes it. Its artists may let the discography find it by title, which the album sync tries
	private async Task<Album> CreateProviderAlbum(ExternalId albumId, Play play, List<Album> knownAlbums, HashSet<ExternalId> artistExternalIds, CancellationToken cancellationToken)
	{
		var knownArtists = (await catalogue.GetArtists(cancellationToken)).ToList();
		var changedArtists = new List<Artist>();
		var album = new Album
		{
			Title = play.AlbumTitle ?? play.TrackTitle,
			Year = 0,
			ExternalIds = [albumId],
			Artists = ChooseArtists(play, knownArtists, artistExternalIds, changedArtists)
				.Select((artist, index) => new AlbumArtist { Artist = artist, Position = index + 1 })
				.ToList(),
		};

		foreach (var artist in changedArtists)
		{
			await catalogue.SaveArtist(artist, cancellationToken);
		}

		await catalogue.SaveAlbum(album, cancellationToken);

		if (album.Artists.Any(albumArtist => albumArtist.Artist.ExternalIds.Any(externalId => externalId.Provider == source.ArtistProvider)))
		{
			var sync = await syncAlbumWithDiscography.Run(album.Id, cancellationToken);

			if (sync.IsRight)
			{
				knownAlbums.RemoveAll(knownAlbum => knownAlbum.Id == sync.GetRight.AbsorbedAlbum?.Id);
				album = sync.GetRight.Album;
			}
			else
			{
				logger.LogDebug("The discography has no album for {Album} by its artists: {Errors}", album, sync.GetLeft);
			}
		}

		knownAlbums.Add(album);

		return album;
	}

	// The artist holding the play artist's id, else the one artist of that name without such an id, else a new untracked artist
	private static IReadOnlyList<Artist> ChooseArtists(Play play, List<Artist> knownArtists, HashSet<ExternalId> artistExternalIds, List<Artist> changedArtists)
	{
		var artists = new List<Artist>();

		foreach (var playArtist in play.Artists)
		{
			var artist = knownArtists.FirstOrDefault(knownArtist => knownArtist.ExternalIds.Contains(playArtist.Id));

			if (artist is null)
			{
				var namesakes = knownArtists
					.Where(knownArtist => MatchKey.For(knownArtist.Name) == MatchKey.For(playArtist.Name))
					.Where(knownArtist => knownArtist.ExternalIds.All(externalId => externalId.Provider != playArtist.Id.Provider))
					.ToList();

				if (namesakes.Count == 1)
				{
					artist = namesakes[0];
					artist.SetExternalId(playArtist.Id);
				}
				else
				{
					artist = new Artist { Name = playArtist.Name, ExternalIds = [playArtist.Id] };
					knownArtists.Add(artist);
				}

				changedArtists.Add(artist);
				artistExternalIds.Add(playArtist.Id);
			}

			artists.Add(artist);
		}

		return artists.Distinct().ToList();
	}

	private bool HoldsDiscographyAlbum(Album album)
	{
		return album.ExternalIds.Any(externalId => externalId.Provider == source.ReleaseProvider || externalId.Provider == source.ReleaseGroupProvider);
	}

	private async Task<Album?> SyncAlbum(Album album, List<Album> knownAlbums, CancellationToken cancellationToken)
	{
		var sync = await syncAlbumWithDiscography.Run(album.Id, cancellationToken);

		if (sync.IsLeft)
		{
			logger.LogWarning("Failed to sync {Album} with the discography: {Errors}", album, sync.GetLeft);
			knownAlbums.Add(album);

			return null;
		}

		var response = sync.GetRight;

		knownAlbums.RemoveAll(knownAlbum => knownAlbum.Id == response.AbsorbedAlbum?.Id);
		knownAlbums.Add(response.Album);

		return response.Album;
	}

	private async Task<ExternalId?> FindRelease(ExternalId albumId, Play play, CancellationToken cancellationToken)
	{
		var linkedReleases = await source.FindLinkedReleases(albumId, cancellationToken);

		if (linkedReleases.Count > 0)
		{
			return linkedReleases[0];
		}

		if (play.Isrc is null || play.AlbumTitle is null)
		{
			return null;
		}

		var releases = (await source.FindRecordings(play.Isrc, cancellationToken))
			.SelectMany(recording => recording.Releases)
			.DistinctBy(release => release.ReleaseId)
			.ToList();
		var titleKey = MatchKey.For(play.AlbumTitle);
		var sameTitle = releases.Where(release => MatchKey.For(release.Title) == titleKey).ToList();

		// An edition named in the title ("Deluxe", "Remastered") still starts with the album's own title
		var editionTitle = releases
			.Where(release => titleKey.StartsWith(MatchKey.For(release.Title), StringComparison.Ordinal) || MatchKey.For(release.Title).StartsWith(titleKey, StringComparison.Ordinal))
			.ToList();
		var candidates = sameTitle.Count > 0 ? sameTitle
			: editionTitle.Count > 0 ? editionTitle
			: releases;
		var releasesByTrackCount = candidates.ToLookup(release => release.TrackCount);

		return candidates
			.OrderByDescending(release => release.IsOfficial)
			.ThenBy(release => string.IsNullOrEmpty(release.Date))
			.ThenBy(release => release.Date, StringComparer.Ordinal)
			.ThenByDescending(release => releasesByTrackCount[release.TrackCount].Count())
			.ThenBy(release => release.ReleaseId.Value, StringComparer.Ordinal)
			.FirstOrDefault()
			?.ReleaseId;
	}

	private static IReadOnlyList<ExternalId> PlayIds(Play play)
	{
		return play.Isrc is null ? [play.TrackId] : [play.TrackId, new Isrc(play.Isrc)];
	}

	private static Track? FindTrack(IReadOnlyList<Track> tracks, Play play)
	{
		var isrcMatch = play.Isrc is null ? null : tracks.FirstOrDefault(track => track.ExternalIds.Contains(new Isrc(play.Isrc)));

		if (isrcMatch is not null)
		{
			return isrcMatch;
		}

		var titleKey = MatchKey.For(play.TrackTitle);
		var samePlace = tracks
			.Where(track => track.DiscNumber == (play.DiscNumber ?? 1) && track.Position == play.TrackNumber)
			.ToList();
		var sameTitle = tracks.Where(track => MatchKey.For(track.Title) == titleKey).ToList();

		return samePlace.FirstOrDefault(track => MatchKey.For(track.Title) == titleKey)
			?? samePlace.FirstOrDefault(track => titleKey.StartsWith(MatchKey.For(track.Title), StringComparison.Ordinal))
			?? (sameTitle.Count == 1 ? sameTitle[0] : null);
	}

	// Gives the play's artist ids to the artists of the same name credited on the track, else on the album
	private static IReadOnlyList<Artist> IdentifyArtists(Play play, Track track, Album album, HashSet<ExternalId> artistExternalIds)
	{
		var creditedArtists = track.Artists.Select(credit => credit.Artist)
			.Concat(album.Artists.Select(credit => credit.Artist))
			.ToList();

		return play.Artists
			.Where(playArtist => !artistExternalIds.Contains(playArtist.Id))
			.Select(playArtist => (PlayArtist: playArtist, Artist: creditedArtists.FirstOrDefault(artist => MatchKey.For(artist.Name) == MatchKey.For(playArtist.Name))))
			.Where(match => match.Artist is not null)
			.Where(match => match.Artist!.ExternalIds.All(externalId => externalId.Provider != match.PlayArtist.Id.Provider))
			.Apply(match => match.Artist!.SetExternalId(match.PlayArtist.Id))
			.Apply(match => artistExternalIds.Add(match.PlayArtist.Id))
			.Select(match => match.Artist!)
			.ToList();
	}
}
