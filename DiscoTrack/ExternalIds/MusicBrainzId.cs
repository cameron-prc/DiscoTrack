namespace DiscoTrack.ExternalIds;

public sealed record MusicBrainzId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.MusicBrainz; }
	}
}
