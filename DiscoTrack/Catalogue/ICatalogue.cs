using DiscoTrack.Albums;
using DiscoTrack.Artists;
using DiscoTrack.ExternalIds;
using DiscoTrack.Tracks;

namespace DiscoTrack.Catalogue;

public interface ICatalogue
{
	Task<Artist?> GetArtist(ArtistId artistId, CancellationToken ct);
	Task<IReadOnlyList<Artist>> GetArtists(CancellationToken ct);
	Task<IReadOnlyList<Album>> GetAlbums(CancellationToken ct);
	Task<Album?> GetAlbum(AlbumId albumId, CancellationToken ct);
	Task<IReadOnlyList<Album>> GetArtistAlbums(ArtistId artistId, CancellationToken ct);
	Task<IReadOnlyList<Artist>> GetAlbumArtists(AlbumId albumId, CancellationToken ct);
	Task<IReadOnlyList<Track>> GetAlbumTracks(AlbumId albumId, CancellationToken ct);

	/// <summary>The album of the track holding each of the given ids. An id no track holds is left out.</summary>
	Task<IReadOnlyDictionary<ExternalId, AlbumId>> FindTrackAlbums(IReadOnlyCollection<ExternalId> trackIds, CancellationToken ct);

	/// <summary>The artist holding each of the given ids. An id no artist holds is left out.</summary>
	Task<IReadOnlyDictionary<ExternalId, ArtistId>> FindArtists(IReadOnlyCollection<ExternalId> artistIds, CancellationToken ct);
	Task SaveArtist(Artist artist, CancellationToken ct);
	Task SaveAlbum(Album album, CancellationToken ct);
	Task SaveAlbumTracks(AlbumId albumId, IReadOnlyList<Track> tracks, CancellationToken ct);
	Task MergeArtists(Artist survivor, ArtistId absorbed, CancellationToken ct);
	Task MergeAlbums(Album survivor, AlbumId absorbed, IReadOnlyList<Track> tracks, CancellationToken ct);
}
