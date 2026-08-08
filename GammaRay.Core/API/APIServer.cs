using GammaRay.Core.API.Services;
using GammaRay.Core.API.Services.Proto;
using Grpc.Core;

namespace GammaRay.Core.API;

public sealed class APIServer(
	APIConfigurationProvider _configurationProvider,
	APIBasicService _service1,
	APIChannelsService _service2,
	APIControlService _service3,
	APIMonitoringService _service4,
	APIServicesService _service5,
	APIFileSystemService _service6,
	APINetworkService _service7
)
{
	private Server? _server;


	public async Task Run(CancellationToken cancellationToken = default)
	{
		var endPoints = _configurationProvider.Configuration.EndPoints;


		_server = new Server()
		{
			Services =
			{
				BasicService.BindService(_service1),
				ChannelsService.BindService(_service2),
				ControlService.BindService(_service3),
				MonitoringService.BindService(_service4),
				ServicesService.BindService(_service5),
				FileSystemService.BindService(_service6),
				NetworkService.BindService(_service7)
			}
		};

		foreach (var endPoint in endPoints)
			_server.Ports.Add(new ServerPort(endPoint.BindAddress.ToString(), endPoint.Port, ServerCredentials.Insecure));

		_server.Start();

		await Task.Delay(-1, cancellationToken);

		await _server.ShutdownAsync();
	}
}
