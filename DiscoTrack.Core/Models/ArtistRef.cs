namespace DiscoTrack.Core.Models;

public class ArtistRef : Artist
{
	public override IReadOnlyList<Album> Albums
		=> throw new InvalidOperationException($"Artist '{Name}' is a reference and has no albums loaded.");

	public override void AddAlbum(Album album)
		=> throw new InvalidOperationException($"Artist '{Name}' is a reference and cannot hold albums.");
}
