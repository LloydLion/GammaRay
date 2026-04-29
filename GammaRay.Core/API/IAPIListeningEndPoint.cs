namespace GammaRay.Core.API;

public delegate Task APIConnectionHandler(APIConnection connection, CancellationToken cancellationToken);

public interface IAPIListeningEndPoint
{
	public Task Run(CancellationToken stopToken = default);

	public void SetConnectionHandler(APIConnectionHandler handler);
}
