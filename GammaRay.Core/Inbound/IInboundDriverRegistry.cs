global using IInboundDriverRegistry = GammaRay.Core.Utils.IDriverRegistry<GammaRay.Core.Inbound.IInboundDriver>;

namespace GammaRay.Core.Inbound;

public static class InboundDriverRegistryExtensions
{
	extension(IInboundDriverRegistry registry)
	{
		public IInbound CreateInboundFromConfiguration(InboundConfiguration inboundConfiguration)
		{
			var driver = registry.ProvideDriver(inboundConfiguration.Protocol);
			var inbound = driver.CreateInbound(inboundConfiguration.EndPoint);
			return inbound;
		}
	}
}
