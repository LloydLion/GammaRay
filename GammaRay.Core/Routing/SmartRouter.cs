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

		NetClientConfiguration config;
		var route = _storage.TryGetRoute(endPoint.Host, profile);

		// Overview:
		// case 1 - No route in storage -> Use last config in queue + start probing
		// case 2 - Is route in storage, but it is expired -> Use expired route + start probing
		// case 3 - Valid route in storage -> just use it, no probing

		if (route is null || route.Value.IsValid == false)
		{
			var category = _domainCategorizer.GetCategoryForDomain(endPoint.Host.DomainName);
			var queueName = _routeGrid.GetConfigurationQueueName(profile, category);
			var queue = _configurations.GetConfigurationQueue(queueName);

			StartBackgroundProbingIfNeed(endPoint.Host, profile, queue);

			config = route is null ? queue.OrderedConfigurations.Last() : _configurations.GetConfiguration(route.Value.ConfigurationName);

			logger.Information("Route for {EndPoint} does not exist in storage." +
				"Router going to try start new probing, now using last config = '{ConfigurationName}' in queue", endPoint, config.Name);
		}
		else config = _configurations.GetConfiguration(route.Value.ConfigurationName);

		return new ProxyRoutingResult([config]);
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

			string? theBestConfigName;
			if (queue.OrderedConfigurations.Count() == 1)
			{
				theBestConfigName = queue.OrderedConfigurations.Single().Name;
			}
			else
			{
				theBestConfigName = await chooseBestConfigurationAsync(site, queue, logger);
				if (theBestConfigName is null) // failed
					return;
			}

			_storage.SaveRoute(site, profile, theBestConfigName);

			logger.Information("Finished probing. Best configuration is '{ConfigurationName}'", theBestConfigName);
		}
		finally
		{
			_probingNow.Remove((profile.Name, site));
		}


		async Task<string?> chooseBestConfigurationAsync(Site site, ClientConfigurationQueue queue, ILogger logger)
		{
			try
			{
				var results = await Task.WhenAll(queue.OrderedConfigurations.Select(config =>
					_prober.ProbeAsync(site, config.Name)
				));

				var theBestIndex = _analyzer.ChooseBestRoute(results);

				if (theBestIndex == -1)
					throw new Exception("Site is unreachable, failed to route.");

				var theBestConfig = queue.OrderedConfigurations.ElementAt(theBestIndex);

				return theBestConfig.Name;
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Failed probing");
				return null;
			}
		}
	}
}
