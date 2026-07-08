using GammaRay.Core.InternetAccess;
using GammaRay.Core.Utils;

namespace GammaRay.Core.Routing;

public sealed class EndPointRoutingConfiguration(string name)
{
	private (InternetAccessPointProvider Provider, InternetAccessPointChain Chain)? _extendedChainCache = null;


	public InternetAccessPointChain IAPChain { get; init; } = InternetAccessPointChain.Empty;

	public RequirementPolicy ChainPolicy { get; init; } = RequirementPolicy.Restricted;

	public string[][] RequiredTags { get; init; } = [];

	public RequirementPolicy TagsPolicy { get; init; } = RequirementPolicy.Restricted;

	public IReadOnlyList<InternetAccessPoint> DefaultIAPChain { get; init; } = [];

	public string Name { get; } = name;


	public InternetAccessPointChain GetExtendedIAPChain(InternetAccessPointProvider internetAccessPointProvider)
	{
		if (ChainPolicy == RequirementPolicy.Restricted)
			return IAPChain;

		if (_extendedChainCache is not null && _extendedChainCache.Value.Provider == internetAccessPointProvider)
			return _extendedChainCache.Value.Chain;

		var extendedChain = IAPChain.Extend(internetAccessPointProvider);
		_extendedChainCache = (internetAccessPointProvider, extendedChain);
		return extendedChain;
	}

	public override string ToString()
	{
		return Name;
	}
}
