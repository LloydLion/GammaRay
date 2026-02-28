using GammaRay.Core.Network.Identity;

namespace GammaRay.Core.Routing.NetworkProfiles;

public interface INetworkProfileMappingRepository
{
	public NetworkProfile GetProfileFor(NetworkIdentity identity);
}
