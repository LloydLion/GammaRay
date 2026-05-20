using GammaRay.Core.InternetAccess;
using System.Diagnostics;
using System.Text;

namespace GammaRay.Core.Services.Probing;

public sealed class ServiceStatusTable(Service service, IReadOnlyDictionary<InternetAccessPoint, ServiceIAPStatus> table)
{
	public Service Service { get; } = service;

	public IReadOnlyDictionary<InternetAccessPoint, ServiceIAPStatus> Table { get; } = table;


	public override string ToString()
	{
		var sb = new StringBuilder();
		bool first = true;
		foreach (var status in Table)
		{
			if (first == false) sb.Append(", ");
			first = false;

			sb.Append(status.Key.Name);
			sb.Append('=');

			switch (status.Value.Type)
			{
				case ServiceIAPStatus.StatusType.Available:
					sb.Append(status.Value.AverageProbeTime.TotalMilliseconds);
					sb.Append("ms");
					break;
				case ServiceIAPStatus.StatusType.ServerSideBan:
					sb.Append(status.Value.AverageProbeTime.TotalMilliseconds);
					sb.Append("ms SSB");
					break;
				case ServiceIAPStatus.StatusType.Blocked:
					sb.Append("Blocked");
					break;
				default: throw new UnreachableException();
			}
		}

		return sb.ToString();
	}


	public ServiceIAPStatus.StatusType CalculateAcceptableStatusType()
	{
		// If via all IAPs we get ServerSideBan (ignoring blocked), we consider it as normal server behavior
		// Else (if at least via one IAP we got Available status) server side ban is considered as a sign of blocking
		//
		// It is typical for Russian services block access only from non Russian IAPs (VPN block)

		if (Table.Values.All(a => a.Type is ServiceIAPStatus.StatusType.ServerSideBan or ServiceIAPStatus.StatusType.Blocked))
			return ServiceIAPStatus.StatusType.ServerSideBan;
		else return ServiceIAPStatus.StatusType.Available;
	}
}
