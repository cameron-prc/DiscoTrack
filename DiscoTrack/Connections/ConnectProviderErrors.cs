namespace DiscoTrack.Connections;

public interface IConnectProviderError;

public record AuthorisationNotStartedHereError : IConnectProviderError;

public record AuthorisationDeniedError(string Reason) : IConnectProviderError;

public record UnableToExchangeCodeError(string Message) : IConnectProviderError;

public record UnableToSaveTokenError(string Message) : IConnectProviderError;
