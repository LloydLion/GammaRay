using GammaRay.Core.Connection.Inbound;
using GammaRay.Core.Network;

namespace GammaRay.Core.Connection;

public readonly record struct ClientConnectionRequest(WebEndPoint TargetEndPoint, IIncomingConnection IncomingConnection);
