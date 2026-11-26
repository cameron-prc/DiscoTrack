using System.Runtime.CompilerServices;
using Dapper;

namespace DiscoTrack.Infrastructure.Sqlite.TypeHandlers;

internal static class SqliteTypeHandlers
{
	[ModuleInitializer]
	internal static void Register()
	{
		SqlMapper.AddTypeHandler(new ArtistIdHandler());
		SqlMapper.AddTypeHandler(new AlbumIdHandler());
		SqlMapper.AddTypeHandler(new TrackIdHandler());
	}
}
