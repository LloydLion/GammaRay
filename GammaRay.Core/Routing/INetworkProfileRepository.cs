using GammaRay.Core.Network;

namespace GammaRay.Core.Routing;

public interface INetworkProfileRepository
{
	public NetworkProfile DefaultProfile { get; }


	public NetworkProfile GetProfileForNetwork(NetworkIdentity network);

	public IEnumerable<NetworkIdentity> ListProfileNetworks(NetworkProfile profile);
}
