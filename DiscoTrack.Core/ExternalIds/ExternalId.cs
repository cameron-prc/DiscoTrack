namespace DiscoTrack.Core.ExternalIds;

public abstract record ExternalId(string Value)
{
	public abstract Provider Provider { get; }

	public sealed override string ToString()
	{
		return Value;
	}
}
