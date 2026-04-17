using GammaRay.Core.InternetAccess;
using GammaRay.Core.Utils;

namespace GammaRay.Core.Routing;

public sealed class EndPointRoutingConfiguration(string name)
{
	private readonly Dictionary<InternetAccessPointProvider, InternetAccessPointChain> _extendedChainsCache = [];


	public InternetAccessPointChain IAPChain { get; init; } = InternetAccessPointChain.Empty;

	public RequirementPolicy ChainPolicy { get; init; } = RequirementPolicy.Restricted;

	public string[][] RequiredTags { get; init; } = [];

	public RequirementPolicy TagsPolicy { get; init; } = RequirementPolicy.Restricted;

	public InternetAccessPointChain DefaultIAPChain { get; init; } = InternetAccessPointChain.Empty;

	public string Name { get; } = name;


	public InternetAccessPointChain GetExtendedIAPChain(InternetAccessPointProvider internetAccessPointProvider)
	{
		if (ChainPolicy == RequirementPolicy.Restricted)
			return IAPChain;

		if (_extendedChainsCache.TryGetValue(internetAccessPointProvider, out var extendedChain))
			return extendedChain;

		extendedChain = IAPChain.Extend(internetAccessPointProvider);
		_extendedChainsCache.Add(internetAccessPointProvider, extendedChain);
		return extendedChain;
	}

	public override string ToString()
	{
		return Name;
	}
}
