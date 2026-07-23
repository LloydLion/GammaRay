using GammaRay.Core.Connection.Inbound;
using System.Net;

namespace GammaRay.Core.Connection;

public readonly record struct ClientNetworkParameters(IPEndPoint RemoteEndPoint, NamedInbound Inbound);
