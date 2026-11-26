namespace DiscoTrack.Listening;

public interface IConnectListeningError;

public record AuthorisationNotStartedHereError : IConnectListeningError;

public record AuthorisationDeniedError(string Reason) : IConnectListeningError;

public record UnableToExchangeCodeError(string Message) : IConnectListeningError;

public record UnableToSaveTokenError(string Message) : IConnectListeningError;
