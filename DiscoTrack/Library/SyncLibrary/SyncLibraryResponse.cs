using DiscoTrack.Albums;
using DiscoTrack.Artists;

namespace DiscoTrack.Library.SyncLibrary;

public record SyncLibraryResponse(IList<Artist> Artists, IList<Album> Albums, IList<SyncFailure> Failures);

public record SyncFailure(SyncAction Action, object Entity, string Reason);

public enum SyncAction
{
	Import,
	Save,
}
