namespace GammaRay.Core.API.Client;

public interface IGammaRayAPIClient
{
	public bool IsConnected { get; }


	public ValueTask ConnectAsync(IAPIEndPointDriver driver, string configurationString);

	public ValueTask DisconnectAsync();


	public ValueTask<byte> RequestAPIVVersionAsync();

	public ValueTask<int> ControlMonitoringAsync(APIConstants.MonitoringMode monitoringMode, Memory<byte>? pendingEventBuffer = null);

	public ValueTask RequestReloadApplicationAsync();

	public ValueTask<string> RequestReadSettingsAsync();

	public ValueTask RequestWriteSettingsAsync(string settingsContent);


	public void AddEventListener(IAPIEventListener listener);

	public void RemoveEventListener(IAPIEventListener listener);
}
