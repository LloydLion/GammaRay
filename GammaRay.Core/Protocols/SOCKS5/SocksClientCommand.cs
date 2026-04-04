namespace GammaRay.Core.Protocols.SOCKS5;

public enum SocksClientCommand : byte
{
	Connect = 0x01,
	Bind = 0x02,
	UDPAssociate = 0x03
}
