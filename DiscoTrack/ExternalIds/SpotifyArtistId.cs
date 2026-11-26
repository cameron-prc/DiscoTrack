namespace DiscoTrack.ExternalIds;

public sealed record SpotifyArtistId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.SpotifyArtist; }
	}
}
