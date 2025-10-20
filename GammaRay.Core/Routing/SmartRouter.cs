using GammaRay.Core.Network;
using GammaRay.Core.Probing;
using GammaRay.Core.Proxy;
using System.Collections.Concurrent;

namespace GammaRay.Core.Routing;

public class SmartRouter(
	IDomainCategorizer _domainCategorizer,
	IConfigurationsProvider _configurations,
	INetworkProfileRepository _profiles,
	IRouteGridProvider _netMap,
	INetworkIdentifier _networkIdentifier,
	ISiteProber _prober,
	IProbeResultsAnalyzer _analyzer
) : IProxyServerRouter
{
	private readonly ConcurrentDictionary<(Site, string), ResolvedDomain?> _routed = new();


	public Task<ProxyRoutingResult> RouteHttpAsync(ProxyContext context, HttpEndPoint targetHost, HttpRequestHeader header) =>
		RouteConnectAsync(context, targetHost);

	public Task<ProxyRoutingResult> RouteConnectAsync(ProxyContext context, HttpEndPoint targetHost)
	{
		return Task.FromResult(RouteConnectAsync(targetHost));
	}

	private ProxyRoutingResult RouteConnectAsync(HttpEndPoint targetHost)
	{
		var currentNetwork = _networkIdentifier.FetchCurrentNetworkIdentity();
		var profile = _profiles.GetProfileForNetwork(currentNetwork);

		bool createdNew = false;
		var resolvedDomain = _routed.GetOrAdd((targetHost.Host, profile.Name), (_) => { createdNew = true; return null; });

		if (createdNew)
			StartBackgroundProbing(targetHost.Host, profile);

		if (resolvedDomain is null)
			return new DirectProxyRoutingResult();

		var config = _configurations.GetConfiguration(resolvedDomain.OptimalConfigurationName);
		if (config.ProxyServer is null)
			return new DirectProxyRoutingResult();
		else
			return new UpstreamProxyRoutingResult(config.ProxyServer);
	}

	private async void StartBackgroundProbing(Site site, NetworkProfile profile)
	{
		await Task.Yield();
		try
		{
			Console.WriteLine($"STARTED PROBING for {profile.Name}\\'{site.DomainName}'");

			var category = _domainCategorizer.GetCategoryForDomain(site.DomainName);

			var queueName = _netMap.GetConfigurationQueueName(profile, category);

			var queue = _configurations.GetConfigurationQueue(queueName);

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

			_routed[(site, profile.Name)] = new ResolvedDomain(theBestConfigName);

			Console.WriteLine($"FINISHED PROBING for {profile.Name}\\'{site.DomainName}' -> {theBestConfigName}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"FAILED PROBING for {profile.Name}\\'{site.DomainName}': {ex}");
			_routed.TryRemove((site, profile.Name), out _);
		}
	}


	private record ResolvedDomain(string OptimalConfigurationName);
}
