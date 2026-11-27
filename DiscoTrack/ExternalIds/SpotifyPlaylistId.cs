namespace DiscoTrack.ExternalIds;

public sealed record SpotifyPlaylistId(string Value) : ExternalId(Value)
{
	public override Provider Provider
	{
		get { return Provider.SpotifyPlaylist; }
	}
}
