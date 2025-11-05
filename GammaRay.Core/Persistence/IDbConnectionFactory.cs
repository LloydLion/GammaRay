using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Data;

namespace GammaRay.Core.Persistence
{
	public interface IDbConnectionFactory
	{
		public IDbConnection CreateNewConnection();
	}

	public class SQLiteConnectionFactory : IDbConnectionFactory
	{
		private readonly Options _options;


		public SQLiteConnectionFactory(IOptions<Options> options)
		{
			_options = options.Value;
		}


		public IDbConnection CreateNewConnection()
		{
			return new SqliteConnection(_options.ConnectionString);
		}


		public class Options
		{
			public required string ConnectionString { get; set; }
		}
	}
}