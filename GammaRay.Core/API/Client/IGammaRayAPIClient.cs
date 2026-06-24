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
	

	public void AddEventListener(IAPIEventListener listener);

	public void RemoveEventListener(IAPIEventListener listener);
}
