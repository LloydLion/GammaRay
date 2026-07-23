namespace GammaRay.Core.Connection.Inbound;

public readonly record struct NamedInbound(IInbound Instance, IInboundDriver Driver, string Name, string DriverName);
