using DiscoTrack.Albums;

namespace DiscoTrack.Discography;

public class DiscographyAlbumResolver(IDiscographySource source) : IDiscographyAlbumResolver
{
	/// <summary>
	/// <para>Finds the discography album a catalogue album refers to. Returns null when it cannot be resolved.</para>
	/// <para>
	/// First attempt to fetch by release or release group
	/// Second if the album artist has a artist id, get the artist and text match the album titles
	/// </para>
	/// </summary>
	public async Task<DiscographyAlbum?> Resolve(Album album, CancellationToken ct)
	{
		var albumId = album.ExternalIds.FirstOrDefault(externalId => externalId.Provider == source.ReleaseProvider)
				?? album.ExternalIds.FirstOrDefault(externalId => externalId.Provider == source.ReleaseGroupProvider);

		if (albumId is not null)
		{
			return await source.GetAlbum(albumId, ct);
		}

		var artistIds = album.Artists
				.OrderBy(albumArtist => albumArtist.Position)
				.SelectMany(albumArtist => albumArtist.Artist.ExternalIds)
				.Where(externalId => externalId.Provider == source.ArtistProvider)
				.Distinct()
				.ToList();
		var titleKey = MatchKey.For(album.Title);

		foreach (var artistId in artistIds)
		{
			var titleMatches = (await source.SearchAlbums(artistId, album.Title, ct))
					.Where(candidate => MatchKey.For(candidate.Title) == titleKey)
					.ToList();

			if (titleMatches.Count == 1)
			{
				return titleMatches[0];
			}

			if (titleMatches.Count > 1)
			{
				var yearMatches = titleMatches.Where(candidate => candidate.Year == album.Year).ToList();

				return yearMatches.Count == 1 ? yearMatches[0] : null;
			}
		}

		return null;
	}
}
