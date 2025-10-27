using Serilog;
using System.Net.Sockets;

namespace GammaRay.Core.Proxy;

public class ProxyRequestContext
{
	public ProxyRequestContext(
		ProxyClientContext clientContext,
		HttpRequestHeader header,
		HttpEndPoint endPoint,
		HttpProxyRequestType requestType,
		int ordinalNumber
	)
	{
		ClientContext = clientContext;
		Header = header;
		EndPoint = endPoint;
		RequestType = requestType;
		RequestOrdinalNumber = ordinalNumber;

		Logger = clientContext.Logger.ForContext("RequestOrd", RequestOrdinalNumber);
	}


	public NetworkStream Stream => ClientContext.Stream;

	public Socket Socket => ClientContext.Socket;

	public ProxyServer Server => ClientContext.Server;

	public long ClientId => ClientContext.ClientId;

	public ProxyClientContext ClientContext { get; }

	public HttpRequestHeader Header { get; }

	public HttpEndPoint EndPoint { get; }

	public HttpProxyRequestType RequestType { get; }

	public int RequestOrdinalNumber { get; }

	public ILogger Logger { get; }


	public override string ToString()
	{
		return $"{ClientContext}:{RequestOrdinalNumber}({RequestType} to {EndPoint})";
	}
}
