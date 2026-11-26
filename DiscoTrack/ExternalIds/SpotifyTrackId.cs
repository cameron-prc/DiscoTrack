namespace DiscoTrack.ExternalIds;

public sealed record SpotifyTrackId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.SpotifyTrack; }
	}
}
