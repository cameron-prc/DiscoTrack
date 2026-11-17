using System.Data;
using Dapper;
using DiscoTrack.Albums;

namespace DiscoTrack.Infrastructure.Sqlite.TypeHandlers;

internal sealed class AlbumIdHandler : SqlMapper.TypeHandler<AlbumId>
{
	public override void SetValue(IDbDataParameter parameter, AlbumId value)
	{
		parameter.Value = value.Value.ToString();
	}

	public override AlbumId Parse(object value)
	{
		return new AlbumId(Guid.Parse((string)value));
	}
}
