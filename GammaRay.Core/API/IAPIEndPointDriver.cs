namespace GammaRay.Core.API;

public interface IAPIEndPointDriver
{
	public IAPIListeningEndPoint CreateListening(string configurationString);

	public ValueTask<IAPIClientConnection> ConnectAsClientAsync(string configurationString);
}
