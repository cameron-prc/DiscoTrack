using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Discography;

public class DiscographyArtistResolver(IDiscographySource source) : IDiscographyArtistResolver
{
	/// <summary>
	/// <para>Finds the discography artist a catalogue artist refers to. Returns null when it cannot be resolved.</para>
	/// <para>
	/// First attempt to fetch by the artist id the artist already holds
	/// Second search artists by name. If the artist has albums, take the one candidate with an album whose title matches one of them
	/// Third if the artist has no albums, take the candidate when there is only one
	/// </para>
	/// </summary>
	public async Task<DiscographyArtist?> Resolve(Artist artist, IReadOnlyList<Album> albums, CancellationToken ct)
	{
		var artistId = artist.ExternalIds.FirstOrDefault(externalId => externalId.Provider == source.ArtistProvider);

		if (artistId is not null)
		{
			return await source.GetArtist(artistId, ct);
		}

		var nameKey = MatchKey.For(artist.Name);
		var candidates = (await source.SearchArtists(artist.Name, ct))
				.Where(candidate => MatchKey.For(candidate.Name) == nameKey)
				.DistinctBy(candidate => candidate.Id)
				.ToList();

		if (albums.Count == 0)
		{
			return candidates.Count == 1 ? candidates[0] : null;
		}

		var titleKeys = albums.Select(album => MatchKey.For(album.Title)).ToHashSet();
		var albumMatches = new List<DiscographyArtist>();

		foreach (var candidate in candidates)
		{
			var candidateAlbums = await source.GetArtistAlbums(candidate.Id, ct);

			if (candidateAlbums.Any(candidateAlbum => titleKeys.Contains(MatchKey.For(candidateAlbum.Title))))
			{
				albumMatches.Add(candidate);
			}
		}

		return albumMatches.Count == 1 ? albumMatches[0] : null;
	}
}
