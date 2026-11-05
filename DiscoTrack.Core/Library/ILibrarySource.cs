namespace DiscoTrack.Core.Library;

public interface ILibrarySource
{
	Task<IReadOnlyList<LibraryAlbum>> GetAlbums(CancellationToken? ct = null);
	Task<IReadOnlyList<LibraryArtist>> GetArtists(CancellationToken? ct = null);
}
