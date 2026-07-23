using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;

namespace GammaRay.Core.Routing;

public readonly record struct RoutingRequest(WebEndPoint Destination, TrackableProcedure TrackableProcedure);
