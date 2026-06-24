using Grpc.Core;

namespace GammaRay.Core.API;

public sealed class APIServer(APIConfigurationProvider configurationProvider, GammaRayApiService apiService)
{
	private readonly APIConfigurationProvider _configurationProvider = configurationProvider;
	private readonly GammaRayApiService _apiService = apiService;
	private Server? _server;


	public async Task Run(CancellationToken cancellationToken = default)
	{
		var endPoints = _configurationProvider.Configuration.EndPoints;


		_server = new Server()
		{
			Services = { Proto.GammaRayService.BindService(_apiService) }
		};

		foreach (var endPoint in endPoints)
			_server.Ports.Add(new ServerPort(endPoint.BindAddress.ToString(), endPoint.Port, ServerCredentials.Insecure));

		_server.Start();

		await Task.Delay(-1, cancellationToken);

		await _server.ShutdownAsync();
	}
}
