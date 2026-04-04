using GammaRay.Core.Network.Flow.Implementation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GammaRay.Core.Protocols.SOCKS5;

public readonly struct SocksClientHelloMessage(ReadOnlyMemory<SocksAuthMethod> supportedAuthMethods)
{
	public SocksClientHelloMessage(SocksAuthMethod[] supportedAuthMethods) : this(supportedAuthMethods.AsMemory()) { }


	public ReadOnlyMemory<SocksAuthMethod> SupportedAuthMethods { get; } = supportedAuthMethods;

	public int BinarySize => SupportedAuthMethods.Length + 2;


	public void Serialize(Span<byte> output)
	{
		var authMethodsAsBytes = MemoryMarshal.AsBytes(SupportedAuthMethods.Span);
		output[0] = SocksConstants.Version;
		output[1] = (byte)SupportedAuthMethods.Length;
		authMethodsAsBytes.CopyTo(output[2..]);
	}

	public byte[] Serialize()
	{
		var buffer = new byte[BinarySize];
		Serialize(buffer);
		return buffer;
	}

	public static async ValueTask<SocksClientHelloMessage> ReadMessageFromSocketAsync(Socket socket, Memory<byte> internalMessageBuffer)
	{
		await socket.ReceiveExactAsync(internalMessageBuffer[..2]);

		if (internalMessageBuffer.Span[0] != SocksConstants.Version)
			throw new Exception("Invalid SOCKS version, only 5 supported");

		var methodCount = internalMessageBuffer.Span[1];

		await socket.ReceiveExactAsync(internalMessageBuffer[..methodCount]);

		var roMem = (ReadOnlyMemory<byte>)internalMessageBuffer[..methodCount];
		var methods = Unsafe.As<ReadOnlyMemory<byte>, ReadOnlyMemory<SocksAuthMethod>>(ref roMem);

		return new SocksClientHelloMessage(methods);
	}
}
