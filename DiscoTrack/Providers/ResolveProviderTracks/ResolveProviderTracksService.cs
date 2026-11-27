using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.Common;
using DiscoTrack.Discography;
using DiscoTrack.Discography.SyncAlbumWithDiscography;
using DiscoTrack.ExternalIds;
using DiscoTrack.Tracks;
using Microsoft.Extensions.Logging;

namespace DiscoTrack.Providers.ResolveProviderTracks;

public class ResolveProviderTracksService(
	IDiscographySource source,
	ICatalogue catalogue,
	ISyncAlbumWithDiscographyService syncAlbumWithDiscography,
	ILogger<ResolveProviderTracksService> logger) : IResolveProviderTracksService
{
	/// <summary>
	/// <para>Makes the catalogue hold the album, track and artists of each track a provider describes, carrying the provider's ids. The provider's track is found again through those ids; nothing else links them.</para>
	/// <para>
	/// An album the catalogue lacks is created untracked from the discography release it resolves to:
	/// first the release the discography links to the provider's album,
	/// second a release of the recording carrying the track's ISRC whose title matches the album's,
	/// third any release of that recording, which is a guess: the track then sits on an album that holds its recording.
	/// </para>
	/// <para>
	/// When the discography answers that it has no such release, the album, its artists and the track are created untracked
	/// from the provider's description alone, holding only the provider's ids. Nothing is created when the discography could not be asked.
	/// </para>
	/// <para>A track that is not on an album holding the discography's tracklist is left unresolved.</para>
	/// </summary>
	public async Task<Either<IList<IResolveProviderTracksError>, ResolveProviderTracksResponse>> Run(IReadOnlyList<ProviderTrack> providerTracks, CancellationToken cancellationToken)
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
			return new List<IResolveProviderTracksError> { new UnableToReadCatalogueError(exception.Message) };
		}

		var resolvedTracks = new List<ProviderTrack>();
		var createdAlbums = new List<Album>();

		foreach (var albumTracks in providerTracks.Where(providerTrack => providerTrack.AlbumId is not null).GroupBy(providerTrack => providerTrack.AlbumId!))
		{
			try
			{
				resolvedTracks.AddRange(await ResolveAlbumTracks(albumTracks.Key, albumTracks.ToList(), knownAlbums, artistExternalIds, createdAlbums, cancellationToken));
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				logger.LogWarning(exception, "Failed to resolve the providerTracks of {AlbumTitle} ({AlbumId})", albumTracks.First().AlbumTitle, albumTracks.Key);
			}
		}

		return new ResolveProviderTracksResponse(resolvedTracks, providerTracks.Except(resolvedTracks).ToList(), createdAlbums);
	}

	private async Task<IReadOnlyList<ProviderTrack>> ResolveAlbumTracks(
		ExternalId albumId,
		IReadOnlyList<ProviderTrack> providerTracks,
		List<Album> knownAlbums,
		HashSet<ExternalId> artistExternalIds,
		List<Album> createdAlbums,
		CancellationToken cancellationToken)
	{
		var album = knownAlbums.FirstOrDefault(knownAlbum => knownAlbum.ExternalIds.Contains(albumId));

		if (album is null)
		{
			var releaseId = await FindRelease(albumId, providerTracks[0], cancellationToken);

			if (releaseId is null)
			{
				logger.LogDebug("The discography has no release for {AlbumTitle} ({AlbumId})", providerTracks[0].AlbumTitle, albumId);

				album = await CreateProviderAlbum(albumId, providerTracks[0], knownAlbums, artistExternalIds, cancellationToken);
				createdAlbums.Add(album);
			}
			else
			{
				var heldAlbum = knownAlbums.FirstOrDefault(knownAlbum => knownAlbum.ExternalIds.Contains(releaseId));

				album = heldAlbum ?? new Album { Title = providerTracks[0].AlbumTitle ?? providerTracks[0].Title, Year = 0, ExternalIds = [releaseId] };
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

		// Only an album without the discography's tracklist takes the provider's tracks: on a listed album an unmatched track is a matching failure
		var takesProviderTracks = !tracks.Any(track => track.ExternalIds.Any(externalId => externalId.Provider == source.RecordingProvider));
		var resolvedTracks = new List<ProviderTrack>();
		var changedArtists = new List<Artist>();
		var tracksChanged = false;
		List<Artist>? knownArtists = null;

		foreach (var providerTrack in providerTracks)
		{
			var track = tracks.FirstOrDefault(candidate => candidate.ExternalIds.Contains(providerTrack.Id)) ?? FindTrack(tracks, providerTrack);

			if (track is null && takesProviderTracks)
			{
				knownArtists ??= (await catalogue.GetArtists(cancellationToken)).ToList();
				track = new Track
				{
					AlbumId = album.Id,
					Title = providerTrack.Title,
					DiscNumber = providerTrack.DiscNumber ?? 1,
					Position = providerTrack.TrackNumber ?? 0,
					DurationMs = providerTrack.DurationMs,
					Artists = ChooseArtists(providerTrack, knownArtists, artistExternalIds, changedArtists)
						.Select((artist, index) => new TrackArtist { Artist = artist, Position = index + 1 })
						.ToList(),
				};
				tracks.Add(track);
			}

			if (track is null)
			{
				logger.LogDebug("{Album} has no track matching {ProviderTrack}", album, providerTrack);

				continue;
			}

			var heldIds = track.ExternalIds.Count;

			track.AddExternalIds(ProviderIds(providerTrack));
			tracksChanged = tracksChanged || track.ExternalIds.Count != heldIds;

			changedArtists.AddRange(IdentifyArtists(providerTrack, track, album, artistExternalIds));
			resolvedTracks.Add(providerTrack);
		}

		foreach (var artist in changedArtists.Distinct())
		{
			await catalogue.SaveArtist(artist, cancellationToken);
		}

		if (tracksChanged)
		{
			await catalogue.SaveAlbumTracks(album.Id, tracks, cancellationToken);
		}

		return resolvedTracks;
	}

	// The album as the provider's track describes it. Its artists may let the discography find it by title, which the album sync tries
	private async Task<Album> CreateProviderAlbum(ExternalId albumId, ProviderTrack providerTrack, List<Album> knownAlbums, HashSet<ExternalId> artistExternalIds, CancellationToken cancellationToken)
	{
		var knownArtists = (await catalogue.GetArtists(cancellationToken)).ToList();
		var changedArtists = new List<Artist>();
		var album = new Album
		{
			Title = providerTrack.AlbumTitle ?? providerTrack.Title,
			Year = 0,
			ExternalIds = [albumId],
			Artists = ChooseArtists(providerTrack, knownArtists, artistExternalIds, changedArtists)
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

	// The artist holding the provider artist's id, else the one artist of that name without such an id, else a new untracked artist
	private static IReadOnlyList<Artist> ChooseArtists(ProviderTrack providerTrack, List<Artist> knownArtists, HashSet<ExternalId> artistExternalIds, List<Artist> changedArtists)
	{
		var artists = new List<Artist>();

		foreach (var providerArtist in providerTrack.Artists)
		{
			var artist = knownArtists.FirstOrDefault(knownArtist => knownArtist.ExternalIds.Contains(providerArtist.Id));

			if (artist is null)
			{
				var namesakes = knownArtists
					.Where(knownArtist => MatchKey.For(knownArtist.Name) == MatchKey.For(providerArtist.Name))
					.Where(knownArtist => knownArtist.ExternalIds.All(externalId => externalId.Provider != providerArtist.Id.Provider))
					.ToList();

				if (namesakes.Count == 1)
				{
					artist = namesakes[0];
					artist.SetExternalId(providerArtist.Id);
				}
				else
				{
					artist = new Artist { Name = providerArtist.Name, ExternalIds = [providerArtist.Id] };
					knownArtists.Add(artist);
				}

				changedArtists.Add(artist);
				artistExternalIds.Add(providerArtist.Id);
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

	private async Task<ExternalId?> FindRelease(ExternalId albumId, ProviderTrack providerTrack, CancellationToken cancellationToken)
	{
		var linkedReleases = await source.FindLinkedReleases(albumId, cancellationToken);

		if (linkedReleases.Count > 0)
		{
			return linkedReleases[0];
		}

		if (providerTrack.Isrc is null || providerTrack.AlbumTitle is null)
		{
			return null;
		}

		var releases = (await source.FindRecordings(providerTrack.Isrc, cancellationToken))
			.SelectMany(recording => recording.Releases)
			.DistinctBy(release => release.ReleaseId)
			.ToList();
		var titleKey = MatchKey.For(providerTrack.AlbumTitle);
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

	private static IReadOnlyList<ExternalId> ProviderIds(ProviderTrack providerTrack)
	{
		return providerTrack.Isrc is null ? [providerTrack.Id] : [providerTrack.Id, new Isrc(providerTrack.Isrc)];
	}

	private static Track? FindTrack(IReadOnlyList<Track> tracks, ProviderTrack providerTrack)
	{
		var isrcMatch = providerTrack.Isrc is null ? null : tracks.FirstOrDefault(track => track.ExternalIds.Contains(new Isrc(providerTrack.Isrc)));

		if (isrcMatch is not null)
		{
			return isrcMatch;
		}

		var titleKey = MatchKey.For(providerTrack.Title);
		var samePlace = tracks
			.Where(track => track.DiscNumber == (providerTrack.DiscNumber ?? 1) && track.Position == providerTrack.TrackNumber)
			.ToList();
		var sameTitle = tracks.Where(track => MatchKey.For(track.Title) == titleKey).ToList();

		return samePlace.FirstOrDefault(track => MatchKey.For(track.Title) == titleKey)
			?? samePlace.FirstOrDefault(track => titleKey.StartsWith(MatchKey.For(track.Title), StringComparison.Ordinal))
			?? (sameTitle.Count == 1 ? sameTitle[0] : null);
	}

	// Gives the provider's artist ids to the artists of the same name credited on the track, else on the album
	private static IReadOnlyList<Artist> IdentifyArtists(ProviderTrack providerTrack, Track track, Album album, HashSet<ExternalId> artistExternalIds)
	{
		var creditedArtists = track.Artists.Select(credit => credit.Artist)
			.Concat(album.Artists.Select(credit => credit.Artist))
			.ToList();

		return providerTrack.Artists
			.Where(providerArtist => !artistExternalIds.Contains(providerArtist.Id))
			.Select(providerArtist => (ProviderArtist: providerArtist, Artist: creditedArtists.FirstOrDefault(artist => MatchKey.For(artist.Name) == MatchKey.For(providerArtist.Name))))
			.Where(match => match.Artist is not null)
			.Where(match => match.Artist!.ExternalIds.All(externalId => externalId.Provider != match.ProviderArtist.Id.Provider))
			.Apply(match => match.Artist!.SetExternalId(match.ProviderArtist.Id))
			.Apply(match => artistExternalIds.Add(match.ProviderArtist.Id))
			.Select(match => match.Artist!)
			.ToList();
	}
}
