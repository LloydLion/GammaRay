using GammaRay.Core.Persistence.Models;
using GammaRay.Core.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace GammaRay.Core.Persistence;

public class RoutePersistenceDbStorage : AsyncDbRepository<(string Site, string Profile), SiteProfileModel>, IRoutePersistenceStorage, IAsyncDisposable
{
	private static readonly ILogger _logger = Log.ForContext<RoutePersistenceDbStorage>();

	private readonly Options _options;


	public RoutePersistenceDbStorage(IOptions<Options> options, AppDbContext dbContext) : base(dbContext, _logger)
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

	protected override async ValueTask ExecuteWriteAsync(AppDbContext context, SiteProfileModel item)
	{
		var affected = await context.Routes
			.Where(s => s.ProfileName == item.ProfileName && s.SiteDomain == item.SiteDomain)
			.ExecuteUpdateAsync(s => s
				.SetProperty(p => p.ConfigurationName, item.ConfigurationName)
				.SetProperty(p => p.ValidUntil, item.ValidUntil)
			);

		if (affected == 0)
		{
			context.Routes.Add(item);
			await context.SaveChangesAsync();
			context.ChangeTracker.Clear();
		}
	}

	protected override IEnumerable<SiteProfileModel> PreloadData(AppDbContext context) => context.Routes.AsNoTracking();

	protected override (string Site, string Profile) ExtractKey(SiteProfileModel value) => (value.SiteDomain, value.ProfileName);


	public class Options
	{
		public TimeSpan RecordTtl { get; set; } = TimeSpan.FromHours(1);
	}
}
