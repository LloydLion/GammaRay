using GammaRay.Core.Network;
using GammaRay.Core.Utils.ValueMatching;

namespace GammaRay.Core.Services;

public sealed class CapabilityDetectionRule
{
	public ValueCondition<TransportType> Transport { get; init; } = NoneValueCondition<TransportType>.AlwaysTrue;

	public ValueCondition<int> Port { get; init; } = NoneValueCondition<int>.AlwaysTrue;

	// TODO: Add preRead options
}
