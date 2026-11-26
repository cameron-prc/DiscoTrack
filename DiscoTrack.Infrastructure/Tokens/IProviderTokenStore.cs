namespace DiscoTrack.Infrastructure.Tokens;

public interface IProviderTokenStore
{
	Task<ProviderToken?> Get(string provider, CancellationToken ct);
	Task Save(string provider, ProviderToken token, CancellationToken ct);
	Task Delete(string provider, CancellationToken ct);
}
