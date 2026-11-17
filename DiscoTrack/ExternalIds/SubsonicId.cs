namespace DiscoTrack.ExternalIds;

public sealed record SubsonicId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.Subsonic; }
	}
}
