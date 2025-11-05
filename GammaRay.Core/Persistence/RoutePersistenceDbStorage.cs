using Dapper;
using GammaRay.Core.Persistence.Models;
using GammaRay.Core.Routing;
using Microsoft.Extensions.Options;
using Serilog;
using System.Data;

namespace GammaRay.Core.Persistence;

public class RoutePersistenceDbStorage : AsyncDbRepository<(string Site, string Profile), SiteProfileModel>, IRoutePersistenceStorage, IAsyncDisposable
{
	private static readonly ILogger _logger = Log.ForContext<RoutePersistenceDbStorage>();

	private readonly Options _options;


	public RoutePersistenceDbStorage(IOptions<Options> options, IDbConnectionFactory connectionFactory) : base(connectionFactory, _logger)
	{
		_options = options.Value;
	}


	public RouteToSite? TryGetRoute(Site site, NetworkProfile profile)
	{
		var value = TryRead((site.DomainName, profile.Name));
		if (value is not null)
			return new RouteToSite(value.ConfigurationName, value.ValidUntil);
		return null;
	}

	public void SaveRoute(Site site, NetworkProfile profile, string optimalConfigurationName)
	{
		var validUntil = DateTime.UtcNow.Add(_options.RecordTtl);

		var model = new SiteProfileModel
		{
			SiteDomain = site.DomainName,
			ProfileName = profile.Name,
			ConfigurationName = optimalConfigurationName,
			ValidUntil = validUntil
		};

		Write(model);
	}

	protected override async ValueTask ExecuteWriteAsync(IDbConnection connection, SiteProfileModel item)
	{
		await connection.ExecuteAsync(
		"""
		INSERT INTO Routes (SiteDomain, ProfileName, ValidUntil, ConfigurationName)
		VALUES (@SiteDomain, @ProfileName, @ValidUntil, @ConfigurationName)
		ON CONFLICT(SiteDomain, ProfileName) DO UPDATE SET
		    ValidUntil = excluded.ValidUntil,
		    ConfigurationName = excluded.ConfigurationName;
		""", item);
	}

	protected override IEnumerable<SiteProfileModel> PreloadData(IDbConnection connection) => connection.Query<SiteProfileModel>("SELECT * FROM Routes");

	protected override (string Site, string Profile) ExtractKey(SiteProfileModel value) => (value.SiteDomain, value.ProfileName);

	protected override void PerformDatabaseMigration(IDbConnection connection)
	{
		connection.Query(
		"""
		CREATE TABLE IF NOT EXISTS Routes (
		    SiteDomain TEXT NOT NULL,
		    ProfileName TEXT NOT NULL,
		    ValidUntil TEXT NOT NULL,
		    ConfigurationName TEXT NOT NULL,
		    PRIMARY KEY (SiteDomain, ProfileName)
		);
		""");
	}


	public class Options
	{
		public TimeSpan RecordTtl { get; set; } = TimeSpan.FromHours(1);
	}
}
