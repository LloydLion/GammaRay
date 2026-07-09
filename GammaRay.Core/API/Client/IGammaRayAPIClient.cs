using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.Network.Profiles;

namespace GammaRay.Core.API.Client;

public interface IGammaRayAPIClient
{
	public bool IsConnected { get; }
	

	public ValueTask ConnectAsync(string hostname, int port);

	public ValueTask DisconnectAsync();
	

	public ValueTask<int> RequestAPIVVersionAsync();

	public ValueTask RequestReloadApplicationAsync();

	public ValueTask<string> RequestReadSettingsAsync();

	public ValueTask RequestWriteSettingsAsync(string settingsContent);

	public ValueTask<IReadOnlyCollection<FullServiceInfoReponse>> QueryServices(ServiceFilter serviceFilter);

	public ValueTask<IReadOnlyCollection<IAPChannelStatusResponse>> QueryChannelStatuses(IAPChannelFilter channelFilter);

	public ValueTask<Network.Identity.NetworkIdentity?> GetCurrentNetworkIdentity();

	public ValueTask<IReadOnlyDictionary<Network.Identity.NetworkIdentity, string?>> QueryNetworkProfileMapping(NetworkProfileMappingFilter filter);

	public ValueTask SetNetworkProfileMapping(string profile, Network.Identity.NetworkIdentity identity);


	public void AddEventListener(IAPIEventListener listener);

	public void RemoveEventListener(IAPIEventListener listener);
}
