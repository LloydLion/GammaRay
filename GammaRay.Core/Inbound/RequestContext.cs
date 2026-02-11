using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.Inbound;

public sealed class RequestContext(WebEndPoint targetEndPoint, IDataFlow incomingDataFlow)
{
	public WebEndPoint TargetEndPoint { get; } = targetEndPoint;

	public IDataFlow IncomingDataFlow { get; } = incomingDataFlow;
}
