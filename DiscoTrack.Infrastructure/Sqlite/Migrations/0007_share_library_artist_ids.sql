ALTER TABLE Artists ADD COLUMN Disambiguation TEXT NULL;

-- The library merges artists that share a name into one, so several catalogue artists
-- can hold the same library id. Every other provider's id still belongs to one artist.
CREATE TABLE ArtistExternalIdsShared (
    ArtistId TEXT NOT NULL REFERENCES Artists(Id) ON DELETE CASCADE,
    Provider TEXT NOT NULL,
    Value TEXT NOT NULL,
    PRIMARY KEY (ArtistId, Provider, Value)
);

INSERT INTO ArtistExternalIdsShared (ArtistId, Provider, Value)
SELECT ArtistId, Provider, Value FROM ArtistExternalIds;

DROP TABLE ArtistExternalIds;

ALTER TABLE ArtistExternalIdsShared RENAME TO ArtistExternalIds;

CREATE UNIQUE INDEX UX_ArtistExternalIds_Provider_Value ON ArtistExternalIds (Provider, Value) WHERE Provider <> 'Subsonic';
