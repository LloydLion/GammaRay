using GammaRay.Core.Network.Identity;

namespace GammaRay.Core.Routing.NetworkProfiles;

public interface INetworkProfileMappingRepository
{
	public NetworkProfile GetProfileFor(NetworkIdentity identity);
}

public static class INetworkProfileMappingRepositoryExtensions
{
	extension(INetworkProfileMappingRepository repository)
	{
		public NetworkProfile? GetProfileForOrNull(NetworkIdentity? identity) => identity is null ? null : repository.GetProfileFor(identity.Value);
	}
}
