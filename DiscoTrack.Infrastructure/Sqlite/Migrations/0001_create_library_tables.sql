CREATE TABLE Artists (
    Id TEXT PRIMARY KEY,
    SubsonicId TEXT NULL,
    Name TEXT NOT NULL
);

CREATE UNIQUE INDEX UX_Artists_SubsonicId ON Artists (SubsonicId);

CREATE TABLE Albums (
    Id TEXT PRIMARY KEY,
    SubsonicId TEXT NULL,
    Title TEXT NOT NULL,
    Year INTEGER NULL,
    IsCompilation INTEGER NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX UX_Albums_SubsonicId ON Albums (SubsonicId);

CREATE TABLE AlbumArtists (
    AlbumId TEXT NOT NULL REFERENCES Albums(Id) ON DELETE CASCADE,
    ArtistId TEXT NOT NULL REFERENCES Artists(Id) ON DELETE CASCADE,
    Position INTEGER NOT NULL,
    PRIMARY KEY (AlbumId, ArtistId),
    UNIQUE (AlbumId, Position)
);
