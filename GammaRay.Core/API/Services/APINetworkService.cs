using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.Network.Identity;
using GammaRay.Core.Network.Profiles;
using Grpc.Core;
using NetworkIdentity = GammaRay.Core.Network.Identity.NetworkIdentity;

namespace GammaRay.Core.API.Services;

public sealed class APINetworkService(
	INetworkIdentifier _networkIdentifier,
	NetworkProfileProvider _profiles,
	INetworkProfileMappingRepository _mapping
) : NetworkService.NetworkServiceBase
{
	public override Task<Proto.NetworkIdentity> GetCurrentNetworkIdentity(Empty request, ServerCallContext context)
	{
		return Task.FromResult(new Proto.NetworkIdentity() { SerializedForm = _networkIdentifier.CurrentIdentity?.SerializedForm });
	}

	public override async Task QueryNetworkProfileMapping(NetworkProfileMappingFilter request, IServerStreamWriter<NetworkProfileMapping> responseStream, ServerCallContext context)
	{
		if (request.NetworkIdentity == string.Empty)
		{
			foreach (var (identity, profile) in _mapping.GetMapping())
			{
				var mapping = new NetworkProfileMapping() { NetworkIdentity = identity.SerializedForm };
				if (profile is not null)
					mapping.NetworkProfile = profile.Name;
				await responseStream.WriteAsync(mapping);
			}
		}
		else
		{
			var identity = new NetworkIdentity(request.NetworkIdentity);
			await responseStream.WriteAsync(new NetworkProfileMapping()
				{ NetworkIdentity = request.NetworkIdentity, NetworkProfile = _mapping.GetProfileFor(identity).Name });
		}
	}

	public override Task<Empty> SetNetworkProfileMapping(NetworkProfileMapping request, ServerCallContext context)
	{
		_mapping.SetProfileFor(new(request.NetworkIdentity), _profiles.Profiles[request.NetworkProfile]);
		return Task.FromResult(new Empty());
	}
}
