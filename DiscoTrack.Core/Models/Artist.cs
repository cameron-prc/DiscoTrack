namespace DiscoTrack.Core.Models;

public abstract class Artist
{
	public required string Name { get; set; }

	public abstract IReadOnlyList<Album> Albums { get; }

	public abstract void AddAlbum(Album album);
}
