using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.Catalogue;

namespace DiscoTrack.Tests.Library;

internal sealed class FakeCatalogue : ICatalogue
{
	public List<Artist> Artists { get; } = [];
	public List<Album> Albums { get; } = [];
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

	private void Record(string method)
	{
		Calls.Add(method);
		if (FailingMethod == method)
		{
			throw new InvalidOperationException($"{nameof(FakeCatalogue)}.{method} failed");
		}
	}
}
