using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.Network;
using GammaRay.Core.Services;
using GammaRay.Core.Services.Probing;
using Grpc.Core;
using System.Diagnostics;
using GammaRay.Core.Utils;
using Google.Protobuf.WellKnownTypes;
using TransportType = GammaRay.Core.Network.TransportType;
using GTransportType = GammaRay.Core.API.Services.Proto.TransportType;

namespace GammaRay.Core.API.Services;

public sealed class APIServicesService(IServiceRepository _services, IServiceStatusTableRepository _serviceStatuses) : ServicesService.ServicesServiceBase
{
	public override async Task QueryFullServiceInfo(ServiceFilter request, IServerStreamWriter<FullServiceInfoReponse> responseStream, ServerCallContext context)
	{
		if (request.Protocol != GTransportType.None && request.Port != 0 && request.HostName != string.Empty)
		{
			var endPoint = new WebEndPoint(new WebHost(request.HostName), request.Port, Convert(request.Protocol));
			var service = _services.TryGetService(endPoint);
			if (service is not null)
				await responseStream.WriteAsync(BuildFullInfo(service.Value));
		}
		else
		{
			var services = _services.ListServices().AsEnumerable();
			if (request.Protocol != GTransportType.None)
			{
				var protocol = Convert(request.Protocol);
				services = services.Where(s => s.Value.EndPoint.Protocol == protocol);
			}

			if (request.Port != 0)
				services = services.Where(s => s.Value.EndPoint.Port == request.Port);

			if (request.HostName != string.Empty)
				services = services.Where(s => s.Value.EndPoint.Host.Domain == request.HostName);


			foreach (var service in services)
				await responseStream.WriteAsync(BuildFullInfo(service));
		}
	}

	private FullServiceInfoReponse BuildFullInfo(Decayable<Service> service)
	{
		var response = new FullServiceInfoReponse
		{
			HostName = service.Value.EndPoint.Host.Domain,
			Port = service.Value.EndPoint.Port,
			Protocol = Convert(service.Value.EndPoint.Protocol),
			CapabilityClass = service.Value.Capability.Class.Name,
			ServiceDecayTime = Timestamp.FromDateTime(service.ValidUntil)
		};

		foreach (var (key, value) in service.Value.Capability.Properties)
			response.CapabilityProperties.Add(key, value);

		var statusTable = _serviceStatuses.TryGetTable(service.Value);
		if (statusTable is not null)
		{
			response.StatusTableDecayTime = Timestamp.FromDateTime(statusTable.Value.ValidUntil);

			foreach (var (IAP, iapStatus) in statusTable.Value.Value.Table)
			{
				response.StatusTableData.Add(IAP.Name, new Proto.ServiceIAPStatus()
				{
					Type = Convert(iapStatus.Type),
					AverageProbeTime = Duration.FromTimeSpan(iapStatus.AverageProbeTime)
				});
			}
		}

		return response;
	}

	private static TransportType Convert(GTransportType type) => type switch
	{
		GTransportType.DatagramBased => TransportType.DatagramBased,
		GTransportType.StreamBased => TransportType.StreamBased,
		_ => throw new UnreachableException()
	};

	private static GTransportType Convert(TransportType type) => type switch
	{
		TransportType.DatagramBased => GTransportType.DatagramBased,
		TransportType.StreamBased => GTransportType.StreamBased,
		_ => throw new UnreachableException()
	};

	private static Proto.ServiceIAPStatus.Types.StatusType Convert(Core.Services.Probing.ServiceIAPStatus.StatusType type) => type switch
	{
		Core.Services.Probing.ServiceIAPStatus.StatusType.Available => Proto.ServiceIAPStatus.Types.StatusType.Available,
		Core.Services.Probing.ServiceIAPStatus.StatusType.ServerSideBan => Proto.ServiceIAPStatus.Types.StatusType.ServerSideBan,
		Core.Services.Probing.ServiceIAPStatus.StatusType.Blocked => Proto.ServiceIAPStatus.Types.StatusType.Blocked,
		_ => throw new UnreachableException()
	};
}
