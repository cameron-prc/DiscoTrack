namespace DiscoTrack.ExternalIds;

public sealed record MusicBrainzRecordingId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.MusicBrainzRecording; }
	}
}
