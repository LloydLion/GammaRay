using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;
using GammaRay.Core.Network.Flow.Implementation;
using GammaRay.Core.Protocols.SOCKS5;
using GammaRay.Core.Utils;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GammaRay.Core.InternetAccess.Channels.Drivers;

[RecommendedDriverName("socks")]
public sealed class SOCKS5ChannelDriver : IChannelDriver
{
	private static readonly SocksClientHelloMessage HelloMessage = new([SocksAuthMethod.NoAuth]);
	private static readonly byte[] HelloMessageBin = HelloMessage.Serialize();


	private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create();


	public async ValueTask<IOpenChannel?> TryOpenChannelAsync(IAPChannel channel, WebEndPoint targetEndPoint)
	{
		var messageBuffer = _pool.Rent(256);
		var addressBuffer = _pool.Rent(256);

		bool isSuccess = false;
		Socket? socket = null;

		try
		{
			socket = await CreateSocket(channel);

			if (await PerformHandshake(messageBuffer, socket))
				return null;

			await RequestServerForConnection(targetEndPoint, messageBuffer, addressBuffer, socket);

			var flow = new SocketBasedStreamDataFlow(socket);
			isSuccess = true;
			return new OpenChannel(flow, socket);
		}
		catch
		{
			return null;
		}
		finally
		{
			_pool.Return(messageBuffer);
			_pool.Return(addressBuffer);

			if (isSuccess == false)
				socket?.Dispose();
		}
	}

	private static async Task RequestServerForConnection(WebEndPoint targetEndPoint, byte[] messageBuffer, byte[] addressBuffer, Socket socket)
	{
		SocksAddressType addressType;
		ReadOnlyMemory<byte> address;

		var destinationHost = targetEndPoint.Host.Domain;
		if (IPAddress.TryParse(destinationHost, out var destinationIPAddress))
		{
			destinationIPAddress.TryWriteBytes(addressBuffer, out var written);
			address = addressBuffer.AsMemory(0, written);
			addressType = destinationIPAddress.AddressFamily switch
			{
				AddressFamily.InterNetwork => SocksAddressType.IPVersion4,
				AddressFamily.InterNetworkV6 => SocksAddressType.IPVersion6,
				var other => throw new NotSupportedException($"Unsupported address family {other}")
			};
		}
		else
		{
			var written = Encoding.ASCII.GetBytes(destinationHost, addressBuffer.AsSpan(1));
			addressBuffer[0] = (byte)written;
			address = addressBuffer.AsMemory(0, written + 1);
			addressType = SocksAddressType.DomainName;
		}

		var requestMessage = new SocksClientRequestMessage(SocksClientCommand.Connect, addressType, address, targetEndPoint.Port);
		var requestMessageLen = requestMessage.Serialize(messageBuffer);
		await socket.SendAsync(messageBuffer.AsMemory(0, requestMessageLen), SocketFlags.None);
		await SocksServerReplyMessage.ReadMessageFromSocketAsync(socket, messageBuffer);
	}

	private static async ValueTask<bool> PerformHandshake(byte[] messageBuffer, Socket socket)
	{
		await socket.SendAsync(HelloMessageBin);
		await socket.ReceiveExactAsync(messageBuffer.AsMemory(0, 2));
		var serverHello = SocksServerHelloMessage.Deserialize(messageBuffer);
		if (serverHello.ChosenMethod != SocksAuthMethod.NoAuth)
			return true;
		return false;
	}

	private static async ValueTask<Socket> CreateSocket(IAPChannel channel)
	{
		var proxyHost = channel.EndPoint.Host.Domain;
		if (!IPAddress.TryParse(proxyHost, out var ipAddress))
			ipAddress = (await Dns.GetHostAddressesAsync(proxyHost)).First();
		var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		await socket.ConnectAsync(ipAddress, channel.EndPoint.Port);
		return socket;
	}


	private class OpenChannel : IOpenChannel
	{
		private readonly IDataFlow _flow;
		private readonly Socket _socket;
		public OpenChannel(IDataFlow flow, Socket socket)
		{
			_flow = flow;
			_socket = socket;
		}
		public ValueTask DisposeAsync()
		{
			_socket.Close();
			_socket.Dispose();
			return ValueTask.CompletedTask;
		}
		public IDataFlow GetFlow() => _flow;
	}
}
