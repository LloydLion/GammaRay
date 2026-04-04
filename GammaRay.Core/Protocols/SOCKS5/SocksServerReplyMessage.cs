using Dapper;
using GammaRay.Core.Network.Flow.Implementation;
using System.Net.Sockets;

namespace GammaRay.Core.Protocols.SOCKS5;

public readonly struct SocksServerReplyMessage(SocksReplyCode code, SocksAddressType addressType, ReadOnlyMemory<byte> address, int port)
{
	public SocksReplyCode Code { get; } = code;

	public SocksAddressType AddressType { get; } = addressType;

	public ReadOnlyMemory<byte> Address { get; } = address;

	public int Port { get; } = port;


	public int Serialize(Span<byte> buffer)
	{
		var offset = 0;

		buffer[offset++] = SocksConstants.Version;
		buffer[offset++] = (byte)Code;
		buffer[offset++] = 0x00; // Reserved
		buffer[offset++] = (byte)AddressType;

		Address.Span.CopyTo(buffer[4..]);
		offset += Address.Length;

		// Most significant byte first order
		buffer[offset++] = (byte)(Port >> 8);
		buffer[offset++] = (byte)(Port & 0xFF);

		return offset;
	}


	public static async ValueTask<SocksServerReplyMessage> ReadMessageFromSocketAsync(Socket socket, Memory<byte> internalMessageBuffer)
	{
		await socket.ReceiveExactAsync(internalMessageBuffer[..4]);

		var code = (SocksReplyCode)internalMessageBuffer.Span[1];
		var addressType = (SocksAddressType)internalMessageBuffer.Span[3];

		var addressLength = addressType.TryGetAddressLength();
		if (addressLength != -1)
			await socket.ReceiveExactAsync(internalMessageBuffer[..addressLength]);
		else if (addressType == SocksAddressType.DomainName)
		{
			await socket.ReceiveExactAsync(internalMessageBuffer[..1]);
			var domainNameLength = internalMessageBuffer.Span[0];
			await socket.ReceiveExactAsync(internalMessageBuffer.Slice(1, domainNameLength));
			addressLength = domainNameLength + 1;
		}
		else throw new NotSupportedException($"Unsupported address type: {addressType}");

		var portBuffer = internalMessageBuffer.Slice(addressLength, 2);
		await socket.ReceiveExactAsync(portBuffer);
		// Port is most significant byte first
		int port = portBuffer.Span[0] << 8 | portBuffer.Span[1];

		return new SocksServerReplyMessage(code, addressType, internalMessageBuffer[..addressLength], port);
	}
}
