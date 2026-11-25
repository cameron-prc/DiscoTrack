using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Discography;

public interface IDiscographyArtistResolver
{
	Task<DiscographyArtist?> Resolve(Artist artist, IReadOnlyList<Album> albums, CancellationToken ct);
}
