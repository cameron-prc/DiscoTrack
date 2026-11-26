namespace DiscoTrack.Infrastructure.Listening;

public interface IPendingAuthorisations
{
	/// <summary>Starts an authorisation and returns the state value that identifies it.</summary>
	string Start();

	/// <summary>True, once, for the state of the authorisation most recently started here.</summary>
	bool Complete(string state);
}
