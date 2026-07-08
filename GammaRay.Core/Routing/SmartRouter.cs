using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Routing.Rules;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using GammaRay.Core.Utils;

namespace GammaRay.Core.Routing;

public sealed class SmartRouter(
	InternetAccessPointProvider _internetAccessPointProvider,

	INetworkIdentifier _networkIdentifier,
	INetworkProfileMappingRepository _networkProfileRepository,
	EndPointCategoriesProvider _endpointCategorizer,
	RoutingRulesProvider _routingRulesProvider,

	IServiceRepository _serviceRepository,
	ICapabilityDetector _capabilityDetector,

	IServiceStatusTableRepository _routeRepository,
	IProbingManager _prober,

	IIAPChannelPicker _channelPicker
) : IRouter
{
	public IAPChannel MakeRoutingDecision(RequestContext context)
	{
		var networkIdentity = _networkIdentifier.CurrentIdentity;
		var networkProfile = _networkProfileRepository.GetProfileForOrNull(networkIdentity) ?? throw new Exception("No internet connection detected");

		var endPointCategory = _endpointCategorizer.Categorize(context.TargetEndPoint);

		var routingContext = new RoutingContext(endPointCategory, networkProfile);
		var routingConfiguration = _routingRulesProvider.Route(routingContext) ?? throw new Exception($"Invalid settings: no route for {routingContext}");
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
		(InternetAccessPoint IAP, IAPChannel Channel) result;

		if (statusTableDec is not null)
		{
			var table = statusTableDec.Value.Value;
			var acceptableStatusType = table.CalculateAcceptableStatusType();
			foreach (var blob in IAPChain.Blobs)
			{
				(InternetAccessPoint, IAPChannel)? bestChannel = null;
				var bestMetric = TimeSpan.MaxValue;
				foreach (var IAP in blob.Points)
				{
					if (table.Table.TryGetValue(IAP, out var serviceStatus) == false || serviceStatus.Type != acceptableStatusType)
						continue;

					var pickResult = _channelPicker.PickBestChannel(IAP, networkProfile, channelRequirements);
					if (pickResult is null or { Status.IsAvailable: false })
						continue;

					var (channelStatus, channel) = pickResult.Value;

					var totalMetric = channelStatus.AverageAccessTime + serviceStatus.AverageProbeTime;
					if (totalMetric < bestMetric)
					{
						bestMetric = totalMetric;
						bestChannel = (IAP, channel);
					}
				}

				if (bestChannel is not null)
					{ result = bestChannel.Value; goto returnResult; }
			}

			// In case of no route fallback to routing using default IAP chain
		}

		foreach (var IAP in routingConfiguration.DefaultIAPChain)
		{
			var status = _channelPicker.PickBestChannel(IAP, networkProfile, channelRequirements);
			if (status is not null and { Status.IsAvailable: true })
				{ result = (IAP, status.Value.Channel); goto returnResult; }
		}

		throw new Exception("Good game, well played, there is just no way to route this shit");

	returnResult:
		CommitReport(result, context, networkProfile, endPointCategory, routingConfiguration, service, statusTableDec);

		return result.Channel;
	}


	private void CommitReport((InternetAccessPoint IAP, IAPChannel Channel) result,
		RequestContext context,
		NetworkProfile profile,
		EndPointCategory endPointCategory,
		EndPointRoutingConfiguration routingConfiguration,
		Service service,
		Decayable<ServiceStatusTable>? statusTable
	)
	{
		var report = new Report
		{
			ResultIAP = result.IAP,
			ResultChannelName = result.IAP.InverseChannels[result.Channel],
			NetworkProfile = profile,
			EndPointCategory = endPointCategory,
			RoutingConfiguration = routingConfiguration,
			CapabilityClass = service.Capability.Class,
			StatusTable = statusTable?.Value
		};

		context.TrackableProcedure.CommitReport(report);
	}


	[SystemReportMetadata(nameof(IRouter), nameof(SmartRouter), "MakeRouteDecision")]
	public class Report() : SystemReport()
	{
		public ReportProperty<InternetAccessPoint> ResultIAP { get; set; }

		public ReportProperty<string> ResultChannelName { get; set; }

		public ReportProperty<NetworkProfile> NetworkProfile { get; set; }

		public ReportProperty<EndPointCategory> EndPointCategory { get; set; }

		public ReportProperty<EndPointRoutingConfiguration> RoutingConfiguration { get; set; }

		public ReportProperty<CapabilityClass> CapabilityClass { get; set; }

		public ReportProperty<ServiceStatusTable?> StatusTable { get; set; }
	}
}
