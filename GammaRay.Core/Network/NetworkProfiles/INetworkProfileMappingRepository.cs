using GammaRay.Core.Network.Identity;

namespace GammaRay.Core.Network.Profiles;

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
