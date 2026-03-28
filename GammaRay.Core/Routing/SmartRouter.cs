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

	IServiceStatusTableRepository _routeRepository,
	IProbingManager _prober,

	IIAPChannelStatusRepository _channelStatusRepository
) : IRouter
{
	public IReadOnlyList<IAPChannel> MakeRoutingDecision(RequestContext context)
	{
		var networkIdentity = _networkIdentifier.CurrentIdentity;
		var networkProfile = _networkProfileRepository.GetProfileFor(networkIdentity);

		var endPointCategory = _endpointCategorizer.Categorize(context.TargetEndPoint);

		var routingConfiguration = _routingGridResolver.GetConfiguration(networkProfile, endPointCategory);


		var serviceDecay = _serviceRepository.TryGetService(context.TargetEndPoint);
		Service service;
		if (serviceDecay is null || serviceDecay.Value.ValidUntil <= context.InitialTime)
		{
			var capability = _capabilityDetector.Detect(context);
			service = new Service(context.TargetEndPoint, capability);
			_serviceRepository.RegisterService(service);
		}
		else service = serviceDecay.Value.Value;


		var routeDec = _routeRepository.TryGetTable(service);
		if (routeDec is null || routeDec.Value.ValidUntil <= context.InitialTime)
			_prober.StartProbing(service, routingConfiguration.GetExtendedIAPChain(_internetAccessPointProvider).PlainListOfPoints, _routeRepository);

		var chain = routeDec is not null ? BuildChainFromRoute(routeDec.Value.Value) : routingConfiguration.DefaultIAPChain;

		var result = chain.Blobs.SelectMany(blob =>
			blob.Points.SelectMany(iap =>
				iap.Channels.Values
					.Select(channel => _channelStatusRepository.GetStatus(iap, channel, networkProfile))
					.Where(s => s.IsAvailable)
			)
			.OrderBy(status => status.AverageAccessTime)
			.Select(status => status.Channel)
		).ToArray();


		return result;
	}

	private InternetAccessPointChain BuildChainFromRoute(ServiceStatusTable route)
	{
		return new(
			route.Table.OrderBy(kv => kv.Value.AverageProbeTime).Select(kv => new InternetAccessPointBlob([kv.Key])).ToArray()
		);
	}
}
