using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;

namespace GammaRay.Core.Routing;

public sealed class SmartRouter(
	InternetAccessPointProvider _internetAccessPointProvider,

	INetworkIdentifier _networkIdentifier,
	INetworkProfileMappingRepository _networkProfileRepository,
	EndPointCategoriesProvider _endpointCategorizer,
	RoutingGridProvider _routingGridResolver,

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
		if (service is null || service.ValidUntil <= context.InitialTime)
		{
			var capability = _capabilityDetector.Detect(context);
			service = new Service(context.TargetEndPoint, capability, context.InitialTime.AddDays(2));
			_serviceRepository.RegisterService(service);
		}


		var route = _routeRepository.TryGetRoute(service);
		if (route is null || route.ValidUntil <= context.InitialTime)
			_prober.StartProbing(service, routingConfiguration.GetExtendedIAPChain(_internetAccessPointProvider), _routeRepository);

		var chain = route is not null ? route.Chain : routingConfiguration.DefaultIAPChain;

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
