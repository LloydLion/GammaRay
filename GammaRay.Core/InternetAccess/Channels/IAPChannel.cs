using GammaRay.Core.Network;
using GammaRay.Core.Routing.NetworkProfiles;
using System.Collections.Immutable;

namespace GammaRay.Core.InternetAccess.Channels
{
	public sealed class IAPChannel(string driverName, GenericWebEndPoint endPoint)
	{
		public string DriverName { get; } = driverName;

		public GenericWebEndPoint EndPoint { get; } = endPoint;

		public IReadOnlyDictionary<string, string> Parameters { get; init; } = ImmutableDictionary<string, string>.Empty;

		public string[] Tags { get; init; } = [];

		public NetworkProfile[] AvailableInNetwork { get; init; } = []; // TODO: create NetworkProfileFilter
	}
}
