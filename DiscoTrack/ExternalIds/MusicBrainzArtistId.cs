namespace DiscoTrack.ExternalIds;

public sealed record MusicBrainzArtistId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.MusicBrainzArtist; }
	}
}
