using DiscoTrack.ExternalIds;
using DiscoTrack.Infrastructure.Clients.Spotify.Models;
using DiscoTrack.Providers;

namespace DiscoTrack.Infrastructure.Providers;

public static class Extensions
{
	/// <summary>The track must have an id: Spotify gives none to a local file.</summary>
	public static ProviderTrack ToProviderTrack(this Track track)
	{
		return new ProviderTrack
		{
			Id = new SpotifyTrackId(track.Id!),
			Title = track.Name,
			Isrc = string.IsNullOrEmpty(track.ExternalIds?.Isrc) ? null : track.ExternalIds.Isrc,
			DurationMs = track.DurationMs,
			DiscNumber = track.DiscNumber,
			TrackNumber = track.TrackNumber,
			Artists = (track.Artists ?? [])
				.Where(artist => artist.Id is not null)
				.Select(artist => new ProviderArtist(new SpotifyArtistId(artist.Id!), artist.Name))
				.ToList(),
			AlbumId = track.Album?.Id is { } albumId ? new SpotifyAlbumId(albumId) : null,
			AlbumTitle = track.Album?.Name,
		};
	}
}
