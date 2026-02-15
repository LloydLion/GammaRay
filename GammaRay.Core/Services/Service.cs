using GammaRay.Core.Network;

namespace GammaRay.Core.Services;

public sealed class Service(WebEndPoint endPoint, Capability capability, DateTime validUntil)
{
	public WebEndPoint EndPoint { get; } = endPoint;

	public Capability Capability { get; } = capability;

	public DateTime ValidUntil { get; } = validUntil;
}
