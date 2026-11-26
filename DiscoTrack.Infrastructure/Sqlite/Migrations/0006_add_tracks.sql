CREATE TABLE Tracks (
    Id TEXT PRIMARY KEY,
    AlbumId TEXT NOT NULL REFERENCES Albums(Id) ON DELETE CASCADE,
    Title TEXT NOT NULL,
    DiscNumber INTEGER NOT NULL,
    Position INTEGER NOT NULL,
    DurationMs INTEGER NULL
);

CREATE INDEX IX_Tracks_AlbumId ON Tracks (AlbumId);

CREATE TABLE TrackExternalIds (
    TrackId TEXT NOT NULL REFERENCES Tracks(Id) ON DELETE CASCADE,
    Provider TEXT NOT NULL,
    Value TEXT NOT NULL,
    PRIMARY KEY (TrackId, Provider, Value)
);

CREATE INDEX IX_TrackExternalIds_Provider_Value ON TrackExternalIds (Provider, Value);

CREATE TABLE TrackArtists (
    TrackId TEXT NOT NULL REFERENCES Tracks(Id) ON DELETE CASCADE,
    ArtistId TEXT NOT NULL REFERENCES Artists(Id) ON DELETE CASCADE,
    Position INTEGER NOT NULL,
    PRIMARY KEY (TrackId, ArtistId),
    UNIQUE (TrackId, Position)
);

CREATE INDEX IX_TrackArtists_ArtistId ON TrackArtists (ArtistId);
