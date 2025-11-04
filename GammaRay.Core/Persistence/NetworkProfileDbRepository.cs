using GammaRay.Core.Network;
using GammaRay.Core.Persistence.Models;
using GammaRay.Core.Routing;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GammaRay.Core.Persistence;

public sealed class NetworkProfileDbRepository : AsyncDbRepository<string, NetworkModel>, INetworkProfileRepository
{
	private static readonly ILogger _logger = Log.ForContext<NetworkProfileDbRepository>();


	private readonly Dictionary<string, NetworkProfile> _profiles;


	public NetworkProfileDbRepository(AppDbContext context, IEnumerable<NetworkProfile> profiles, string defaultProfileName)
		: base(context, _logger)
	{
		_profiles = profiles.ToDictionary(s => s.Name);
		DefaultProfile = _profiles[defaultProfileName];
	}


	public NetworkProfile DefaultProfile { get; }


	public NetworkProfile GetProfileForNetwork(NetworkIdentity network)
	{
		var networkId = network.SerializeToString();

		var model = TryRead(networkId);

		if (model is null)
		{
			Write(new NetworkModel { Identity = networkId, UsedProfile = null });
			return DefaultProfile;
		}
		else if (model.UsedProfile is null)
			return DefaultProfile;
		else
			return _profiles[model.UsedProfile];
	}

	public IEnumerable<NetworkIdentity> ListProfileNetworks(NetworkProfile profile)
	{
		throw new NotImplementedException();
	}

	protected override async ValueTask ExecuteWriteAsync(AppDbContext context, NetworkModel item)
	{
		context.Add(item);
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();
	}

	protected override string ExtractKey(NetworkModel value) => value.Identity;

	protected override IEnumerable<NetworkModel> PreloadData(AppDbContext context) => context.Networks.AsNoTracking();
}
