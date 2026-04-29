namespace GammaRay.Core.API.Client;

public interface IGammaRayAPIClient
{
	public bool IsConnected { get; }


	public ValueTask ConnectAsync(IAPIEndPointDriver driver, string configurationString);

	public ValueTask DisconnectAsync();


	public ValueTask<byte> RequestAPIVVersionAsync();

	public ValueTask ControlMonitoringAsync(APIConstants.MonitoringMode monitoringMode);

	public ValueTask RequestReloadApplicationAsync();

	public ValueTask<string> RequestReadSettingsAsync();

	public ValueTask RequestWriteSettingsAsync(string settingsContent);


	public void AddEventListener(IAPIEventListener listener);

	public void RemoveEventListener(IAPIEventListener listener);
}
