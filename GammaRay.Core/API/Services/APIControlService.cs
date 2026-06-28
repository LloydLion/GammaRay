using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.Host;
using Grpc.Core;

namespace GammaRay.Core.API.Services;

public sealed class APIControlService(ApplicationControl _applicationControl) : ControlService.ControlServiceBase
{
	public override Task<Empty> ReloadApplication(Empty request, ServerCallContext context)
	{
		_applicationControl.Restart();
		return Task.FromResult(new Empty());
	}
}
