using GammaRay.Core.Network;
using GammaRay.Core.Probing;
using GammaRay.Core.Proxy;
using Serilog;

namespace GammaRay.Core.Routing;

public class SmartRouter(
	IDomainCategorizer _domainCategorizer,
	IConfigurationsProvider _configurations,
	INetworkProfileRepository _profiles,
	IRouteGridProvider _routeGrid,
	INetworkIdentifier _networkIdentifier,
	ISiteProber _prober,
	IProbeResultsAnalyzer _analyzer,
	IRoutePersistenceStorage _storage
) : IProxyServerRouter
{
	private static readonly ILogger _logger = Log.ForContext<SmartRouter>();

	private readonly HashSet<(string Profile, Site Site)> _probingNow = [];


	public Task<ProxyRoutingResult> RouteRequestAsync(ProxyRequestContext requestContext) =>
		Task.FromResult(RouteRequest(requestContext));

	private ProxyRoutingResult RouteRequest(ProxyRequestContext requestContext)
	{
		var logger = requestContext.Logger.ForContext<SmartRouter>();
		var endPoint = requestContext.EndPoint;

		logger.Debug("Routing new request to {EndPoint}", endPoint);
		var currentNetwork = _networkIdentifier.CurrentIdentity;
		var profile = _profiles.GetProfileForNetwork(currentNetwork);
		var category = _domainCategorizer.GetCategoryForDomain(endPoint.Host.DomainName);
		var queue = _configurations.GetConfigurationQueue(_routeGrid.GetConfigurationQueueName(profile, category));

		if (queue.OrderedConfigurations is [var singleConfiguration])
		{
			return new ProxyRoutingResult([singleConfiguration]);
		}

		IEnumerable<NetClientConfiguration> configs;
		var route = _storage.TryGetRoute(endPoint.Host, profile);

		// Overview:
		// case 1 - No route in storage -> Use last config in queue + start probing
		// case 2 - Is route in storage, but it is expired -> Use expired route + start probing
		// case 3 - Valid route in storage -> just use it, no probing

		if (route is null || route.Value.IsValid == false)
		{

			StartBackgroundProbingIfNeed(endPoint.Host, profile, queue);

			if (route is null)
			{
				var config = queue.OrderedConfigurations[^1];
				configs = [config];
				logger.Information("Route for {EndPoint} does not exist in storage. " +
					"Router going to try start new probing, now using last config = '{ConfigurationName}' in queue", endPoint, config.Name);
			}
			else
			{
				configs = route.Value.ConfigurationsNames.Select(_configurations.GetConfiguration).ToArray();

				logger.Information("Route for {EndPoint} does exist in storage, but it outdated. " +
					"Router going to try start new probing, now using it: {Configurations}", endPoint, configs);
			}
		}
		else configs = route.Value.ConfigurationsNames.Select(_configurations.GetConfiguration).ToArray();

		return new ProxyRoutingResult(configs);
	}

	private async void StartBackgroundProbingIfNeed(Site site, NetworkProfile profile, ClientConfigurationQueue queue)
	{
		var logger = _logger.ForContext("NetworkProfile", profile.Name).ForContext("Site", site.DomainName);

		if (_probingNow.Add((profile.Name, site)) == false)
		{
			logger.Debug("Probing is already running. New probing will not be started");
			return;
		}

		try
		{
			logger.Information("Started probing", profile.Name, site);

            string[]? bestConfigurations = await chooseBestConfigurationAsync(site, queue, logger);
			if (bestConfigurations is null) // failed
				return;
			if (bestConfigurations is { Length: 0 }) // no route -> use last (default) configuration
				bestConfigurations = [queue.OrderedConfigurations[^1].Name];
			

			_storage.SaveRoute(site, profile, bestConfigurations);

			logger.Information("Finished probing. Best configuration is '{ConfigurationName}'", bestConfigurations);
		}
		finally
		{
			_probingNow.Remove((profile.Name, site));
		}


		async Task<string[]?> chooseBestConfigurationAsync(Site site, ClientConfigurationQueue queue, ILogger logger)
		{
			try
			{
				var results = await Task.WhenAll(queue.OrderedConfigurations.Select(config =>
					_prober.ProbeAsync(site, config.Name)
				));

				var bestConfigurations = _analyzer.ChooseBestRoutes(results);

				return bestConfigurations.Select(s => s.UsedConfiguration.Name).ToArray();
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Failed probing");
				return null;
			}
		}
	}
}
