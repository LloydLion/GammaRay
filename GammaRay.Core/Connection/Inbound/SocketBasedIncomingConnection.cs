using GammaRay.Core.Network.Flow;
using System.Net.Sockets;

namespace GammaRay.Core.Connection.Inbound;

public sealed class SocketBasedIncomingConnection(Socket _socket, IDataFlow _flow) : IIncomingConnection
{
	public IDataFlow GetFlow() => _flow;


	public void ResetConnection()
	{
		_socket.Close();
	}
}
