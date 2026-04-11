using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Utils;

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

	IIAPChannelPicker _channelPicker,

	EndPointRoutingConfigurationProvider _endPointRoutingConfigurationProvider,
	CapabilityClassProvider _capabilityClassProvider
) : IRouter
{
	public IAPChannel MakeRoutingDecision(RequestContext context)
	{
		var networkIdentity = _networkIdentifier.CurrentIdentity;
		var networkProfile = _networkProfileRepository.GetProfileFor(networkIdentity);

		var endPointCategory = _endpointCategorizer.Categorize(context.TargetEndPoint);

		var routingConfiguration = _routingGridResolver.GetConfiguration(networkProfile, endPointCategory);
		var IAPChain = routingConfiguration.GetExtendedIAPChain(_internetAccessPointProvider);


		var serviceDecay = _serviceRepository.TryGetService(context.TargetEndPoint);
		Service service;
		if (serviceDecay is null || serviceDecay.Value.ValidUntil <= context.InitialTime)
		{
			var capability = _capabilityDetector.Detect(context);
			service = new Service(context.TargetEndPoint, capability);
			_serviceRepository.RegisterService(service);
		}
		else service = serviceDecay.Value.Value;


		var statusTableDec = _routeRepository.TryGetTable(service);
		if (statusTableDec is null || statusTableDec.Value.IsValid(context.InitialTime) == false)
			_prober.StartProbing(service, IAPChain.PlainListOfPoints, _routeRepository);


		var channelRequirements = new IAPChannelRequirements() { RequiredTags = routingConfiguration.RequiredTags };
		IAPChannel result;

		if (statusTableDec is not null)
		{
			var table = statusTableDec.Value.Value;
			foreach (var blob in IAPChain.Blobs)
			{
				IAPChannel? bestChannel = null;
				var bestMetric = TimeSpan.MaxValue;
				foreach (var IAP in blob.Points)
				{
					if (table.Table.TryGetValue(IAP, out var serviceStatus) == false || serviceStatus.IsUnavailable)
						continue;

					var channelStatus = _channelPicker.PickBestChannel(IAP, networkProfile, channelRequirements);
					if (channelStatus is null or { IsAvailable: false })
						continue;

					var totalMetric = channelStatus.AverageAccessTime + serviceStatus.AverageProbeTime;
					if (totalMetric < bestMetric)
					{
						bestMetric = totalMetric;
						bestChannel = channelStatus.Channel;
					}
				}

				if (bestChannel is not null)
					{ result = bestChannel; goto returnResult; }
			}

			// In case of no route fallback to routing using default IAP chain
		}

		foreach (var blob in routingConfiguration.DefaultIAPChain.Blobs)
		{
			foreach (var IAP in blob.Points)
			{
				var status = _channelPicker.PickBestChannel(IAP, networkProfile, channelRequirements);
				if (status is not null and { IsAvailable: true })
					{ result = status.Channel; goto returnResult; }
			}
		}

		throw new Exception("Good game, well played, there is just no way to route this shit");

	returnResult:
		PrintReport(result, context, networkProfile, endPointCategory, routingConfiguration, service, statusTableDec);

		return result;
	}


	private void PrintReport(IAPChannel result,
		RequestContext context,
		NetworkProfile profile,
		EndPointCategory endPointCategory,
		EndPointRoutingConfiguration routingConfiguration,
		Service service,
		Decayable<ServiceStatusTable>? statusTable
	)
	{
		var resultIAP = _internetAccessPointProvider.PlainInternetAccessPoints.First(iap => iap.Channels.Values.Contains(result));

		Console.WriteLine("== Routing decision report: ");
		Console.WriteLine("\tResult: " + $"{resultIAP.Name}/{resultIAP.Channels.Single(s => s.Value == result).Key}");

		Console.WriteLine("\tEndpoint: " + context.TargetEndPoint);
		Console.WriteLine("\tNetworkProfile: " + profile.Name);
		Console.WriteLine("\tEndpointCategory: " + endPointCategory.Name);
		var name = _endPointRoutingConfigurationProvider.EndPointRoutingConfigurations.Single(kv => kv.Value == routingConfiguration).Key;
		Console.WriteLine("\tRoutingConfiguration: " + name);
		name = _capabilityClassProvider.CapabilityClasses.Single(kv => kv.Value == service.Capability.Class).Key;
		Console.WriteLine("\tCapability: " + name);
		if (statusTable is not null)
			Console.WriteLine("\tStatusTable: " + string.Join(", ", statusTable.Value.Value.Table
				.Select(s => $"{s.Key.Name}={(s.Value.IsAvailable ? s.Value.AverageProbeTime.TotalMilliseconds : "INF")}ms")
			));
		else Console.WriteLine("\tStatusTable: None");
		Console.WriteLine("\tTime: " + context.InitialTime);
	}
}
