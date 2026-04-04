namespace GammaRay.Core.Protocols.SOCKS5;

public enum SocksAddressType : byte
{
	IPVersion4 = 0x01,
	DomainName = 0x03,
	IPVersion6 = 0x04
}

public static class SocksAddressTypeExtensions
{
	extension(SocksAddressType addressType)
	{
		public int TryGetAddressLength()
		{
			return addressType switch
			{
				SocksAddressType.IPVersion4 => 4,
				SocksAddressType.IPVersion6 => 16,
				_ => -1
			};
		}
	}
}
