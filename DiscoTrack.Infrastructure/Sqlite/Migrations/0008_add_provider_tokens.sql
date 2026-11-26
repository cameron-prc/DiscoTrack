-- AccessToken and RefreshToken hold text protected by the application's key ring, never the tokens themselves.
CREATE TABLE ProviderTokens (
    Provider TEXT PRIMARY KEY,
    AccessToken TEXT NOT NULL,
    RefreshToken TEXT NOT NULL,
    ExpiresAt TEXT NOT NULL
);
