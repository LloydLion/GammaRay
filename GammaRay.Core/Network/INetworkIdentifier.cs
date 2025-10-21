using System.Runtime.InteropServices;

namespace GammaRay.Core.Network;

public interface INetworkIdentifier
{
	public OSPlatform TargetPlatform { get; }

	public DateTime LastRefresh { get; }

	public NetworkIdentity CurrentIdentity { get; }
}
