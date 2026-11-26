CREATE TABLE ArtistScans (
    ArtistId TEXT NOT NULL REFERENCES Artists(Id) ON DELETE CASCADE,
    Provider TEXT NOT NULL,
    ScannedAt TEXT NOT NULL,
    PRIMARY KEY (ArtistId, Provider)
);
