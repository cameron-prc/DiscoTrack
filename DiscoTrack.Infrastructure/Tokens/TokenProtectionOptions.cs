namespace DiscoTrack.Infrastructure.Tokens;

public class TokenProtectionOptions
{
	public const string Section = "TokenProtection";

	public string KeysPath { get; set; } = "keys";
}
