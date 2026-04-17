using GammaRay.Core.Network;

namespace GammaRay.Core.Services;

public sealed class Service(WebEndPoint endPoint, Capability capability)
{
	public WebEndPoint EndPoint { get; } = endPoint;

	public Capability Capability { get; } = capability;


	public override string ToString()
	{
		return $"{Capability.Class} service {EndPoint}";
	}
}
