using DiscoTrack.ExternalIds;
using DiscoTrack.Library;

namespace DiscoTrack.Tests.Library;

internal sealed class FakeLibrarySource : ILibrarySource
{
	public List<LibraryAlbum> Albums { get; } = [];
	public List<LibraryArtist> Artists { get; } = [];
	public List<LibraryTrack> Tracks { get; } = [];
	public List<string> Calls { get; } = [];
	public string? FailingMethod { get; set; }

	public Provider Provider
	{
		get { return Provider.Subsonic; }
	}

	public Task<IReadOnlyList<LibraryAlbum>> GetAlbums(CancellationToken ct)
	{
		Record(nameof(GetAlbums));
		return Task.FromResult<IReadOnlyList<LibraryAlbum>>(Albums.ToList());
	}

	public Task<IReadOnlyList<LibraryArtist>> GetArtists(CancellationToken ct)
	{
		Record(nameof(GetArtists));
		return Task.FromResult<IReadOnlyList<LibraryArtist>>(Artists.ToList());
	}

	public Task<IReadOnlyList<LibraryTrack>> GetTracks(CancellationToken ct)
	{
		Record(nameof(GetTracks));
		return Task.FromResult<IReadOnlyList<LibraryTrack>>(Tracks.ToList());
	}

	private void Record(string method)
	{
		Calls.Add(method);
		if (FailingMethod == method)
		{
			throw new InvalidOperationException($"{nameof(FakeLibrarySource)}.{method} failed");
		}
	}
}
