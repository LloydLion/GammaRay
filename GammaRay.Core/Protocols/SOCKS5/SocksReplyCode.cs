namespace GammaRay.Core.Protocols.SOCKS5;

public enum SocksReplyCode
{
	Succeeded = 0x00,
	GeneralSOCKSServerFailure = 0x01,
	connectionNotAllowedByRuleset = 0x02,
	NetworkUnreachable = 0x03,
	HostUnreachable = 0x04,
	ConnectionRefused = 0x05,
	TTLExpired = 0x06,
	CommandNotSupported = 0x07,
	AddressTypeNotSupported = 0x08
}
