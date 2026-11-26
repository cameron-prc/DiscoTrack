using DiscoTrack.Common;

namespace DiscoTrack.Listening;

public interface IListeningConnection
{
	bool IsConfigured { get; }

	Task<bool> IsConnected(CancellationToken ct);

	/// <summary>Returns the address to send the user to so they can grant access.</summary>
	string StartAuthorisation();

	/// <summary>Finishes the authorisation with the values the provider sent back to the redirect address.</summary>
	Task<Either<IList<IConnectListeningError>, ListeningConnected>> CompleteAuthorisation(string? code, string? state, string? error, CancellationToken ct);

	Task Disconnect(CancellationToken ct);
}
