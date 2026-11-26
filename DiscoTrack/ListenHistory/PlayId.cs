namespace DiscoTrack.ListenHistory;

public readonly record struct PlayId(Guid Value)
{
	public static PlayId New()
	{
		return new PlayId(Guid.CreateVersion7());
	}

	public override string ToString()
	{
		return Value.ToString();
	}
}
