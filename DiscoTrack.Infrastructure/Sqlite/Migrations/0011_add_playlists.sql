-- A playlist as its provider describes it. SnapshotId is the provider's marker for the current contents;
-- ImportedSnapshotId is the marker the stored items were copied at.
CREATE TABLE Playlists (
    Id TEXT PRIMARY KEY,
    Provider TEXT NOT NULL,
    Value TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    OwnerName TEXT NULL,
    SnapshotId TEXT NULL,
    ItemCount INTEGER NOT NULL,
    Tracked INTEGER NOT NULL,
    ImportedSnapshotId TEXT NULL,
    LastImportedAt TEXT NULL,
    RemovedAt TEXT NULL,
    UNIQUE (Provider, Value)
);

CREATE TABLE PlaylistItems (
    PlaylistId TEXT NOT NULL REFERENCES Playlists(Id) ON DELETE CASCADE,
    Position INTEGER NOT NULL,
    AddedAt TEXT NULL,
    TrackProvider TEXT NOT NULL,
    TrackValue TEXT NOT NULL,
    TrackTitle TEXT NOT NULL,
    Isrc TEXT NULL,
    DurationMs INTEGER NULL,
    DiscNumber INTEGER NULL,
    TrackNumber INTEGER NULL,
    AlbumProvider TEXT NULL,
    AlbumValue TEXT NULL,
    AlbumTitle TEXT NULL,
    PRIMARY KEY (PlaylistId, Position)
);

CREATE INDEX IX_PlaylistItems_Track ON PlaylistItems (TrackProvider, TrackValue);

CREATE TABLE PlaylistItemArtists (
    PlaylistId TEXT NOT NULL,
    ItemPosition INTEGER NOT NULL,
    Position INTEGER NOT NULL,
    Provider TEXT NOT NULL,
    Value TEXT NOT NULL,
    Name TEXT NOT NULL,
    PRIMARY KEY (PlaylistId, ItemPosition, Position),
    FOREIGN KEY (PlaylistId, ItemPosition) REFERENCES PlaylistItems(PlaylistId, Position) ON DELETE CASCADE
);
