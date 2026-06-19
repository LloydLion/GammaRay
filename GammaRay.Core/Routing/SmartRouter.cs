using GammaRay.Core.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Monitoring;
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

	IIAPChannelPicker _channelPicker
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

		foreach (var blob in routingConfiguration.DefaultIAPChain.Blobs)
		{
			foreach (var IAP in blob.Points)
			{
				var status = _channelPicker.PickBestChannel(IAP, networkProfile, channelRequirements);
				if (status is not null and { Status.IsAvailable: true })
					{ result = (IAP, status.Value.Channel); goto returnResult; }
			}
		}

		throw new Exception("Good game, well played, there is just no way to route this shit");

	returnResult:
		PrintReport(result, context, networkProfile, endPointCategory, routingConfiguration, service, statusTableDec);

		return result.Channel;
	}


	private void PrintReport((InternetAccessPoint IAP, IAPChannel Channel) result,
		RequestContext context,
		NetworkProfile profile,
		EndPointCategory endPointCategory,
		EndPointRoutingConfiguration routingConfiguration,
		Service service,
		Decayable<ServiceStatusTable>? statusTable
	)
	{
		using var report = context.MonitoringContext.NewReport<Report>();

		report.ResultIAP = result.IAP;
		report.ResultChannelName = result.IAP.InverseChannels[result.Channel];
		report.NetworkProfile = profile;
		report.EndPointCategory = endPointCategory;
		report.RoutingConfiguration = routingConfiguration;
		report.CapabilityClass = service.Capability.Class;

		if (statusTable is not null)
			report.StatusTable = statusTable.Value.Value;
		else report.StatusTable = null;
	}


	public class Report() : SystemReport(nameof(SmartRouter))
	{
		public ReportProperty<InternetAccessPoint> ResultIAP { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<string> ResultChannelName { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<NetworkProfile> NetworkProfile { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<EndPointCategory> EndPointCategory { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<EndPointRoutingConfiguration> RoutingConfiguration { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<CapabilityClass> CapabilityClass { get; set => SetProperty(ref field, value.Value); }

		public ReportProperty<ServiceStatusTable?> StatusTable { get; set => SetProperty(ref field, value.Value); }
	}
}
