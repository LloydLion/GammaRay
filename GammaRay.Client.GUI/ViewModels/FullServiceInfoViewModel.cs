using GammaRay.Core.Network;
using GammaRay.Core.Services.Probing;
using System.Diagnostics;

namespace GammaRay.Client.GUI.ViewModels;

public sealed class FullServiceInfoViewModel(GenericWebEndPoint endPoint, string capabilityClass, IEnumerable<KeyValuePair<string, ServiceIAPStatus>> statusTable, TimeSpan remainingTime)
{
	public string EndPoint { get; } = $"{endPoint.Host}:{endPoint.Port}";

	public string CapabilityClass { get; } = capabilityClass;

	public string StatusTable { get; } = string.Join(" ", statusTable.Select(kv => $"{kv.Key}={GetStatusLetter(kv.Value)}{kv.Value.AverageProbeTime.TotalMilliseconds}"));

	public TimeSpan RemainingTime { get; } = remainingTime;

	private static char GetStatusLetter(ServiceIAPStatus status) => status.Type switch
	{
		ServiceIAPStatus.StatusType.Available => 'a',
		ServiceIAPStatus.StatusType.ServerSideBan => 's',
		ServiceIAPStatus.StatusType.Blocked => 'b',
		_ => throw new UnreachableException()
	};
}
