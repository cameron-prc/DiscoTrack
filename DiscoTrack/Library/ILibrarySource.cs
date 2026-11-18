using DiscoTrack.ExternalIds;

namespace DiscoTrack.Library;

public interface ILibrarySource
{
	Provider Provider { get; }

	Task<IReadOnlyList<LibraryAlbum>> GetAlbums(CancellationToken ct);
	Task<IReadOnlyList<LibraryArtist>> GetArtists(CancellationToken ct);
}
