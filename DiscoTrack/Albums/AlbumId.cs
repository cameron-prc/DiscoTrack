namespace DiscoTrack.Albums;

public readonly record struct AlbumId(Guid Value)
{
	public static AlbumId New()
	{
		return new AlbumId(Guid.CreateVersion7());
	}

	public override string ToString()
	{
		return Value.ToString();
	}
}
