using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Utils;

namespace GammaRay.Core.Routing;

public sealed class SmartRouter(
	ITimeService _time,
	IInternetAccessPointProvider _internetAccessPointProvider,

	INetworkIdentifier _networkIdentifier,
	INetworkProfileRepository _networkProfileRepository,
	IEndpointCategorizer _endpointCategorizer,
	IRoutingGridResolver _routingGridResolver,

	IServiceRepository _serviceRepository,
	ICapabilityDetector _capabilityDetector,
	
	IServiceRouteRepository _routeRepository,
	IProber _prober,

	IIAPChannelStatusRepository _channelStatusRepository
) : IRouter
{
	public IReadOnlyList<IAPChannel> MakeRoutingDecision(RequestContext context)
	{
		var networkIdentity = _networkIdentifier.CurrentIdentity;
		var networkProfile = _networkProfileRepository.GetProfileFor(networkIdentity);
		
		var endPointCategory = _endpointCategorizer.Categorize(context.TargetEndPoint);

		var routingConfiguration = _routingGridResolver.GetConfiguration(networkProfile, endPointCategory);

		
		var service = _serviceRepository.TryGetService(context.TargetEndPoint);
		if (service is null || service.ValidUntil <= _time.Now)
		{
			var capability = _capabilityDetector.Detect(context);
			service = new Service(context.TargetEndPoint, capability, _time.Now.AddDays(2));
			_serviceRepository.RegisterService(service);
		}


		var route = _routeRepository.TryGetRoute(service);
		if (route is null || route.ValidUntil <= _time.Now)
			_prober.StartProbing(service, routingConfiguration.GetExtendedIAPChain(_internetAccessPointProvider), _routeRepository);

		var chain = route?.Chain;
		chain ??= routingConfiguration.DefaultIAPChain;


		var result = chain.Blobs.SelectMany(blob =>
			blob.Points.SelectMany(iap =>
				iap.Channels.Values
					.Select(channel => _channelStatusRepository.GetStatus(iap, channel, networkProfile))
					.Where(s => s.IsAvailable)
			)
			.OrderBy(status => status.Metric)
			.Select(status => status.Channel)
		).ToArray();


		return result;
	}
}
