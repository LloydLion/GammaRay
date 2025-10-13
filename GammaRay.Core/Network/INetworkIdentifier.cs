using System.Runtime.InteropServices;

namespace GammaRay.Core.Network;

public interface INetworkIdentifier
{
	public OSPlatform TargetPlatform { get; }

	public NetworkIdentity FetchCurrentNetworkIdentity();
}
