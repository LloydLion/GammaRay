using GammaRay.Core.Network;
using GammaRay.Core.Utils.ValueMatching;

namespace GammaRay.Core.Settings.Model;

public sealed class SettingsCapabilityClassModel
{
	public required DetectionRule[] DetectionRules;
	public required CapabilityProbingMethod ProbingMethod;

	public sealed class DetectionRule
	{
		public ValueCondition<TransportType>? Transport;
		public ValueCondition<int>? Port;
	}

	public sealed class CapabilityProbingMethod
	{
		public required string Driver;
		public required SD<string> Parameters;
	}
}
