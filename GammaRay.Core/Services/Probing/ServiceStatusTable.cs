using GammaRay.Core.InternetAccess;
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
			if (status.Value.IsAvailable)
			{
				sb.Append(status.Value.AverageProbeTime.TotalMilliseconds);
				sb.Append("ms");
			}
			else sb.Append("Unavailable");
		}

		return string.Join(", ", Table.Select(s => $"{s.Key.Name}={(s.Value.IsAvailable ? s.Value.AverageProbeTime.TotalMilliseconds : "INF")}ms"));
	}
}
