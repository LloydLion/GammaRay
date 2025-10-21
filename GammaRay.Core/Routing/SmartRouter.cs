using GammaRay.Core.Network;
using GammaRay.Core.Probing;
using GammaRay.Core.Proxy;

namespace GammaRay.Core.Routing;

public class SmartRouter(
	IDomainCategorizer _domainCategorizer,
	IConfigurationsProvider _configurations,
	INetworkProfileRepository _profiles,
	IRouteGridProvider _netMap,
	INetworkIdentifier _networkIdentifier,
	ISiteProber _prober,
	IProbeResultsAnalyzer _analyzer,
	IRoutePersistenceStorage _storage
) : IProxyServerRouter
{
	private readonly HashSet<(string Profile, Site Site)> _probingNow = new();


	public Task<ProxyRoutingResult> RouteHttpAsync(ProxyContext context, HttpEndPoint targetHost, HttpRequestHeader header) =>
		RouteConnectAsync(context, targetHost);

	public Task<ProxyRoutingResult> RouteConnectAsync(ProxyContext context, HttpEndPoint targetHost)
	{
		return Task.FromResult(RouteConnectAsync(targetHost));
	}

	private ProxyRoutingResult RouteConnectAsync(HttpEndPoint targetHost)
	{
		var currentNetwork = _networkIdentifier.CurrentIdentity;
		var profile = _profiles.GetProfileForNetwork(currentNetwork);

		NetClientConfiguration config;
		var route = _storage.TryGetRoute(targetHost.Host, profile);
		if (route is null)
		{
			lock (_probingNow)
			{
				bool shouldStartNewProbingTask = _probingNow.Add((profile.Name, targetHost.Host));

				var category = _domainCategorizer.GetCategoryForDomain(targetHost.Host.DomainName);
				var queueName = _netMap.GetConfigurationQueueName(profile, category);
				var queue = _configurations.GetConfigurationQueue(queueName);

				if (shouldStartNewProbingTask)
					StartBackgroundProbing(targetHost.Host, profile, queue);

				config = queue.OrderedConfigurations.Last();
			}
		}
		else config = _configurations.GetConfiguration(route);

		if (config.ProxyServer is null)
			return new DirectProxyRoutingResult();
		else
			return new UpstreamProxyRoutingResult(config.ProxyServer);
	}

	private async void StartBackgroundProbing(Site site, NetworkProfile profile, ClientConfigurationQueue queue)
	{
		try
		{
			Console.WriteLine($"STARTED PROBING for {profile.Name}\\'{site.DomainName}'");


			string theBestConfigName;
			if (queue.OrderedConfigurations.Count() != 1)
			{
				var results = await Task.WhenAll(queue.OrderedConfigurations.Select(config =>
					_prober.ProbeAsync(site, config.Name)
				));

				var theBestIndex = _analyzer.ChooseBestRoute(results);

				if (theBestIndex == -1)
					throw new Exception("Site is unreachable, failed to route.");

				var theBestConfig = queue.OrderedConfigurations.ElementAt(theBestIndex);

				theBestConfigName = theBestConfig.Name;
			}
			else
			{
				theBestConfigName = queue.OrderedConfigurations.Single().Name;
			}

			_storage.SaveRoute(site, profile, theBestConfigName);

			Console.WriteLine($"FINISHED PROBING for {profile.Name}\\'{site.DomainName}' -> {theBestConfigName}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"FAILED PROBING for {profile.Name}\\'{site.DomainName}': {ex}");
		}
		finally
		{
			lock (_probingNow)
			{
				_probingNow.Remove((profile.Name, site));
			}
		}
	}


	private record ResolvedDomain(string OptimalConfigurationName);
}
