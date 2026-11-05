using System.Data;
using Dapper;
using DiscoTrack.Core.Artists;

namespace DiscoTrack.Infrastructure.Sqlite.TypeHandlers;

internal sealed class ArtistIdHandler : SqlMapper.TypeHandler<ArtistId>
{
	public override void SetValue(IDbDataParameter parameter, ArtistId value)
	{
		parameter.Value = value.Value.ToString();
	}

	public override ArtistId Parse(object value)
	{
		return new ArtistId(Guid.Parse((string)value));
	}
}
