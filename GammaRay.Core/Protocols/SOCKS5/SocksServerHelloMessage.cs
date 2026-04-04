namespace GammaRay.Core.Protocols.SOCKS5;

public readonly struct SocksServerHelloMessage(SocksAuthMethod chosenMethod)
{
	public const int FixedBinLength = 2;


	public SocksAuthMethod ChosenMethod { get; } = chosenMethod;


	public void Serialize(Span<byte> output)
	{
		output[0] = SocksConstants.Version;
		output[1] = (byte)ChosenMethod;
	}

	public static SocksServerHelloMessage Deserialize(ReadOnlySpan<byte> buffer)
	{
		if (buffer[0] != SocksConstants.Version)
			throw new ArgumentException($"Socks version is not {SocksConstants.Version}");

		return new SocksServerHelloMessage((SocksAuthMethod) buffer[1]);
	}
}
