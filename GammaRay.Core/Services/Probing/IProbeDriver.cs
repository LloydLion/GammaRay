using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network;
using GammaRay.Core.Network.Flow;

namespace GammaRay.Core.Services.Probing;

public interface IProbeDriver
{
	public Task<ProbeResult> ProbeAsync(
		IDataFlow targetOutcomingFlow,
		WebEndPoint endPoint,
		IReadOnlyDictionary<string, string> parameters,
		CommonProbeDriverOptions options
	);
}
