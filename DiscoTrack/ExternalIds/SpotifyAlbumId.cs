namespace DiscoTrack.ExternalIds;

public sealed record SpotifyAlbumId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.SpotifyAlbum; }
	}
}
