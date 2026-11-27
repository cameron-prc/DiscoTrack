using DiscoTrack.ExternalIds;

namespace DiscoTrack.Playlists;

/// <summary>A provider's playlist, kept as the provider describes it. Its items are only copied while it is tracked.</summary>
public class Playlist
{
	public PlaylistId Id { get; init; } = PlaylistId.New();
	public required ExternalId ExternalId { get; init; }
	public required string Name { get; set; }
	public string? Description { get; set; }
	public string? OwnerName { get; set; }

	/// <summary>The provider's marker for the playlist's contents as last listed. It changes whenever the playlist does.</summary>
	public string? SnapshotId { get; set; }
	public int ItemCount { get; set; }
	public bool Tracked { get; set; }

	/// <summary>The marker the playlist carried when its items were last copied.</summary>
	public string? ImportedSnapshotId { get; set; }
	public DateTimeOffset? LastImportedAt { get; set; }

	/// <summary>When the provider stopped listing the playlist. The playlist and its items are kept.</summary>
	public DateTimeOffset? RemovedAt { get; set; }

	public bool HasChangedSinceImport
		=> LastImportedAt is null || SnapshotId is null || SnapshotId != ImportedSnapshotId;

	public override string ToString()
	{
		return $"{Name} ({Id})";
	}
}
