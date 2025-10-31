using GammaRay.Core.Persistence.Models;
using GammaRay.Core.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Threading.Channels;

namespace GammaRay.Core.Persistence;

public class RoutePersistenceStorage : IRoutePersistenceStorage, IAsyncDisposable
{
	private readonly ILogger _logger = Log.ForContext<RoutePersistenceStorage>();

	private readonly Options _options;
	private readonly AppDbContext _dbContext;
	private Dictionary<(string Site, string Profile), SiteProfileModel>? _data;
	private readonly Channel<SiteProfileModel> _writeChannel;
	private Task? _writerTask;


	public RoutePersistenceStorage(IOptions<Options> options, AppDbContext dbContext)
	{
		_options = options.Value;

		_writeChannel = Channel.CreateUnbounded<SiteProfileModel>(
			new UnboundedChannelOptions()
			{
				SingleReader = true,
				SingleWriter = false
			}
		);

		_dbContext = dbContext;
	}


	private Dictionary<(string Site, string Profile), SiteProfileModel> Data => _data ??
		throw new InvalidOperationException("RoutePersistenceStorage not initialized. Call Initialize() before use.");


	public void Initialize()
	{
		_writerTask = Task.Run(WriterLoopAsync);

		_data = _dbContext.Routes
			.AsNoTracking()
			.ToDictionary(s => (s.SiteDomain, s.ProfileName));
	}

	public RouteToSite? TryGetRoute(Site site, NetworkProfile profile)
	{
		if (Data.TryGetValue((site.DomainName, profile.Name), out var val))
			return new RouteToSite(val.ConfigurationName, val.ValidUntil);
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

		Data[(site.DomainName, profile.Name)] = model;
		_writeChannel.Writer.TryWrite(model);
	}

	private async Task WriterLoopAsync()
	{
		try
		{
			while (await _writeChannel.Reader.WaitToReadAsync())
			{
				while (_writeChannel.Reader.TryRead(out var item))
				{
					var affected = await _dbContext.Routes
						.Where(s => s.ProfileName == item.ProfileName && s.SiteDomain == item.SiteDomain)
						.ExecuteUpdateAsync(s => s
							.SetProperty(p => p.ConfigurationName, item.ConfigurationName)
							.SetProperty(p => p.ValidUntil, item.ValidUntil)
						);

					if (affected == 0)
					{
						_dbContext.Routes.Add(item);
						await _dbContext.SaveChangesAsync();
						_dbContext.ChangeTracker.Clear();
					}
				}
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			_logger.Error(ex, "WriterLoop failed");
		}
	}

	public async ValueTask DisposeAsync()
	{
		_writeChannel.Writer.Complete();
		if (_writerTask is not null)
			await _writerTask;
	}


	public class Options
	{
		public TimeSpan RecordTtl { get; set; } = TimeSpan.FromHours(1);
	}
}
