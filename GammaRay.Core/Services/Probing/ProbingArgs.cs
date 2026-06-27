using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.Services.Probing;

public readonly record struct ProbingArgs(
	IDataFlow TargetOutcomingFlow,
	WebEndPoint EndPoint,
	IReadOnlyDictionary<string, string> Parameters,
	CommonProbeDriverOptions Options,
	TimeProvider TimeProvider,
	TrackableProcedure MonitoringContext
);
