using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;
using DiscoTrack.ExternalIds;
using DiscoTrack.Tracks;

namespace DiscoTrack.Tests.Library;

internal sealed class FakeCatalogue : ICatalogue
{
	public List<Artist> Artists { get; } = [];
	public List<Album> Albums { get; } = [];
	public Dictionary<AlbumId, IReadOnlyList<Track>> Tracks { get; } = [];
	public List<string> Calls { get; } = [];
	public string? FailingMethod { get; set; }

	public Task<IReadOnlyList<Artist>> GetArtists(CancellationToken ct)
	{
		Record(nameof(GetArtists));
		return Task.FromResult<IReadOnlyList<Artist>>(Artists.ToList());
	}

	public Task<IReadOnlyList<Album>> GetAlbums(CancellationToken ct)
	{
		Record(nameof(GetAlbums));
		return Task.FromResult<IReadOnlyList<Album>>(Albums.ToList());
	}

	public Task<IReadOnlyList<Album>> GetArtistAlbums(ArtistId artistId, CancellationToken ct)
	{
		Record(nameof(GetArtistAlbums));
		return Task.FromResult<IReadOnlyList<Album>>(Albums.Where(a => a.Artists.Any(c => c.Artist.Id == artistId)).ToList());
	}

	public Task<Artist?> GetArtist(ArtistId artistId, CancellationToken ct)
	{
		Record(nameof(GetArtist));
		return Task.FromResult(Artists.FirstOrDefault(a => a.Id == artistId));
	}

	public Task<Album?> GetAlbum(AlbumId albumId, CancellationToken ct)
	{
		Record(nameof(GetAlbum));
		return Task.FromResult(Albums.FirstOrDefault(a => a.Id == albumId));
	}

	public Task<IReadOnlyList<Artist>> GetAlbumArtists(AlbumId albumId, CancellationToken ct)
	{
		Record(nameof(GetAlbumArtists));
		return Task.FromResult<IReadOnlyList<Artist>>(Albums.Where(a => a.Id == albumId).SelectMany(a => a.Artists).OrderBy(c => c.Position).Select(c => c.Artist).ToList());
	}

	public Task SaveArtist(Artist artist, CancellationToken ct)
	{
		Record(nameof(SaveArtist));
		if (!Artists.Contains(artist))
		{
			Artists.Add(artist);
		}
		return Task.CompletedTask;
	}

	public Task SaveAlbum(Album album, CancellationToken ct)
	{
		Record(nameof(SaveAlbum));
		if (!Albums.Contains(album))
		{
			Albums.Add(album);
		}
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<Track>> GetAlbumTracks(AlbumId albumId, CancellationToken ct)
	{
		Record(nameof(GetAlbumTracks));
		return Task.FromResult(Tracks.GetValueOrDefault(albumId, []));
	}

	public Task SaveAlbumTracks(AlbumId albumId, IReadOnlyList<Track> tracks, CancellationToken ct)
	{
		Record(nameof(SaveAlbumTracks));
		Tracks[albumId] = tracks;
		return Task.CompletedTask;
	}

	public Task<IReadOnlyDictionary<ExternalId, AlbumId>> FindTrackAlbums(IReadOnlyCollection<ExternalId> trackIds, CancellationToken ct)
	{
		Record(nameof(FindTrackAlbums));
		return Task.FromResult<IReadOnlyDictionary<ExternalId, AlbumId>>(Tracks.Values
			.SelectMany(tracks => tracks)
			.SelectMany(track => track.ExternalIds.Where(trackIds.Contains).Select(externalId => (ExternalId: externalId, track.AlbumId)))
			.DistinctBy(found => found.ExternalId)
			.ToDictionary(found => found.ExternalId, found => found.AlbumId));
	}

	public Task<IReadOnlyDictionary<ExternalId, ArtistId>> FindArtists(IReadOnlyCollection<ExternalId> artistIds, CancellationToken ct)
	{
		Record(nameof(FindArtists));
		return Task.FromResult<IReadOnlyDictionary<ExternalId, ArtistId>>(Artists
			.SelectMany(artist => artist.ExternalIds.Where(artistIds.Contains).Select(externalId => (ExternalId: externalId, artist.Id)))
			.DistinctBy(found => found.ExternalId)
			.ToDictionary(found => found.ExternalId, found => found.Id));
	}

	public Task MergeArtists(Artist survivor, ArtistId absorbed, CancellationToken ct)
	{
		Record(nameof(MergeArtists));
		Artists.RemoveAll(a => a.Id == absorbed);
		return Task.CompletedTask;
	}

	public Task MergeAlbums(Album survivor, AlbumId absorbed, IReadOnlyList<Track> tracks, CancellationToken ct)
	{
		Record(nameof(MergeAlbums));
		Albums.RemoveAll(a => a.Id == absorbed);
		Tracks.Remove(absorbed);
		Tracks[survivor.Id] = tracks;
		return Task.CompletedTask;
	}

	private void Record(string method)
	{
		Calls.Add(method);
		if (FailingMethod == method)
		{
			throw new InvalidOperationException($"{nameof(FakeCatalogue)}.{method} failed");
		}
	}
}
