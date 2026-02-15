using GammaRay.Core.Network.Identity;

namespace GammaRay.Core.Routing;

public interface INetworkProfileRepository
{
	public NetworkProfile DefaultProfile { get; }

	public NetworkProfile GetProfileFor(NetworkIdentity identity);
}
