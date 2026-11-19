namespace DiscoTrack.ExternalIds;

public sealed record MusicBrainzReleaseId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.MusicBrainzRelease; }
	}
}
