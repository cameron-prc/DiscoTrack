CREATE TABLE ArtistExternalIds (
    ArtistId TEXT NOT NULL REFERENCES Artists(Id) ON DELETE CASCADE,
    Provider TEXT NOT NULL,
    Value TEXT NOT NULL,
    PRIMARY KEY (Provider, Value)
);

CREATE INDEX IX_ArtistExternalIds_ArtistId ON ArtistExternalIds (ArtistId);

CREATE TABLE AlbumExternalIds (
    AlbumId TEXT NOT NULL REFERENCES Albums(Id) ON DELETE CASCADE,
    Provider TEXT NOT NULL,
    Value TEXT NOT NULL,
    PRIMARY KEY (Provider, Value)
);

CREATE INDEX IX_AlbumExternalIds_AlbumId ON AlbumExternalIds (AlbumId);

INSERT INTO ArtistExternalIds (ArtistId, Provider, Value)
SELECT Id, 'Subsonic', SubsonicId FROM Artists WHERE SubsonicId IS NOT NULL;

INSERT INTO AlbumExternalIds (AlbumId, Provider, Value)
SELECT Id, 'Subsonic', SubsonicId FROM Albums WHERE SubsonicId IS NOT NULL;

DROP INDEX UX_Artists_SubsonicId;
ALTER TABLE Artists DROP COLUMN SubsonicId;

DROP INDEX UX_Albums_SubsonicId;
ALTER TABLE Albums DROP COLUMN SubsonicId;
