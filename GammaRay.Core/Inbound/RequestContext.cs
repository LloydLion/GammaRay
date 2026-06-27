using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.Inbound;

public sealed class RequestContext(WebEndPoint targetEndPoint, IDataFlow incomingDataFlow, DateTime initialTime, TrackableProcedure procedure)
{
	public WebEndPoint TargetEndPoint { get; } = targetEndPoint;

	public IDataFlow IncomingDataFlow { get; } = incomingDataFlow;

	public DateTime InitialTime { get; } = initialTime;

	public TrackableProcedure TrackableProcedure { get; } = procedure;
}
