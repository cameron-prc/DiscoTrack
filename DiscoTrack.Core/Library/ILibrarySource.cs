namespace DiscoTrack.Core.Library;

public interface ILibrarySource
{
	Task<IReadOnlyList<LibraryAlbum>> GetAlbums(CancellationToken ct);
	Task<IReadOnlyList<LibraryArtist>> GetArtists(CancellationToken ct);
}
