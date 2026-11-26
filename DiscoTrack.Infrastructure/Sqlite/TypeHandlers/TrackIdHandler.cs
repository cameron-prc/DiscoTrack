using System.Data;
using Dapper;
using DiscoTrack.Tracks;

namespace DiscoTrack.Infrastructure.Sqlite.TypeHandlers;

internal sealed class TrackIdHandler : SqlMapper.TypeHandler<TrackId>
{
	public override void SetValue(IDbDataParameter parameter, TrackId value)
	{
		parameter.Value = value.Value.ToString();
	}

	public override TrackId Parse(object value)
	{
		return new TrackId(Guid.Parse((string)value));
	}
}
