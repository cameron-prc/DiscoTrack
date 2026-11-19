namespace DiscoTrack.ExternalIds;

public sealed record MusicBrainzReleaseGroupId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.MusicBrainzReleaseGroup; }
	}
}
