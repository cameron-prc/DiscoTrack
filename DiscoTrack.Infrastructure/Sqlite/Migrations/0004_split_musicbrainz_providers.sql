UPDATE ArtistExternalIds SET Provider = 'MusicBrainzArtist' WHERE Provider = 'MusicBrainz';

UPDATE AlbumExternalIds SET Provider = 'MusicBrainzRelease' WHERE Provider = 'MusicBrainz';
