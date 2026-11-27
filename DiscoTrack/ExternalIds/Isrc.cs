namespace DiscoTrack.ExternalIds;

/// <summary>The code a label gives a recording. A track can hold several, and tracks on different albums share one.</summary>
public sealed record Isrc(string Value) : ExternalId(Value.ToUpperInvariant())
{
	public override Provider Provider
	{
		get { return Provider.Isrc; }
	}
}
