using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Network.Flow.Implementation;
using GammaRay.Core.Utils;
using System.Net;
using System.Net.Sockets;

namespace GammaRay.Core.InternetAccess.Channels;

[RecommendedDriverName("local")]
public sealed class LocalChannelDriver : IChannelDriver
{
	public async ValueTask<IOpenChannel?> TryOpenChannelAsync(IAPChannel channel, TransportType requestedTransportType)
	{
		try
		{
			var ipAddress = Dns.GetHostAddresses(channel.EndPoint.Host.Domain).First();
			var ipEndPoint = new IPEndPoint(ipAddress, channel.EndPoint.Port);

			switch (requestedTransportType)
			{
				case TransportType.StreamBased:
					{
						var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
						await socket.ConnectAsync(ipEndPoint);
						var flow = new SocketBasedStreamDataFlow(socket);
						return new OpenChannel(flow, socket);
					}
				case TransportType.DatagramBased:
					{
						var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
						var flow = new SocketBasedDatagramDataFlow(socket, ipEndPoint);
						return new OpenChannel(flow, socket);
					}
				default:
					throw new NotSupportedException();
			}
		}
		catch (SocketException)
		{
			return null;
		}
	}


	private class OpenChannel(IDataFlow flow, Socket socket) : IOpenChannel
	{
		private readonly IDataFlow _flow = flow;
		private readonly Socket _socket = socket;

		public ValueTask DisposeAsync()
		{
			_socket.Close();
			_socket.Dispose();
			return ValueTask.CompletedTask;
		}

		public IDataFlow GetFlow()
		{
			return _flow;
		}
	}
}
