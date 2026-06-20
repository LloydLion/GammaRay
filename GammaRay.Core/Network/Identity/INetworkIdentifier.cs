namespace GammaRay.Core.Network.Identity;

public interface INetworkIdentifier
{
	public DateTime LastRefresh { get; }

	public NetworkIdentity? CurrentIdentity { get; }


	public IDisposable SubscribeForChanges(Action<INetworkIdentifier> callback);
}
