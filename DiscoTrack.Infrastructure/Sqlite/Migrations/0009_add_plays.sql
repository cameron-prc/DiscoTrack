-- PlayedAt is UTC to the millisecond in a fixed-width form, so text order is time order.
-- A track played at an instant is one play: the unique key is what stops a play being copied twice.
CREATE TABLE Plays (
    Id TEXT PRIMARY KEY,
    PlayedAt TEXT NOT NULL,
    TrackProvider TEXT NOT NULL,
    TrackValue TEXT NOT NULL,
    TrackTitle TEXT NOT NULL,
    Isrc TEXT NULL,
    DurationMs INTEGER NULL,
    AlbumProvider TEXT NULL,
    AlbumValue TEXT NULL,
    AlbumTitle TEXT NULL,
    UNIQUE (TrackProvider, TrackValue, PlayedAt)
);

CREATE INDEX IX_Plays_PlayedAt ON Plays (PlayedAt);

CREATE TABLE PlayArtists (
    PlayId TEXT NOT NULL REFERENCES Plays(Id) ON DELETE CASCADE,
    Position INTEGER NOT NULL,
    Provider TEXT NOT NULL,
    Value TEXT NOT NULL,
    Name TEXT NOT NULL,
    PRIMARY KEY (PlayId, Position)
);
