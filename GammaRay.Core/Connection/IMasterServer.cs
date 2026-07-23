using GammaRay.Core.Connection.Inbound;
using System.Net;

namespace GammaRay.Core.Connection;

public interface IMasterServer
{
	public Task Run(NamedInbound[] inbounds, CancellationToken cancellationToken);
}

public interface IMasterServerInboundAgent
{
	public ClientConnection CreateBlankConnection(IPEndPoint remoteEndPoint);

	public void HandleFatalError(ClientConnection connection, Exception exception);

	public Task HandleRequest(ClientConnection connection, ClientConnectionRequest request);
}
