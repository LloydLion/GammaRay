using GammaRay.Core.API.Services.Proto;
using Grpc.Core;

namespace GammaRay.Core.API.Services;

public sealed class APIBasicService : BasicService.BasicServiceBase
{
	public override Task<VersionResponse> GetAPIVersion(Empty request, ServerCallContext context)
	{
		return Task.FromResult(new VersionResponse() { Version = APIConstants.APIVersion });
	}
}
