using GammaRay.Core.Settings.Model;

namespace GammaRay.Core.Network.Profiles;

public sealed class NetworkProfileProvider
{
	public const string DefaultProfileName = "default";


	public NetworkProfileProvider(SettingsModelRoot modelRoot)
	{
		var rawProfiles = modelRoot.NetworkProfiles.Select(cm => new NetworkProfile(cm.Key)).ToArray();

		if (rawProfiles.Any(s => s.Name == DefaultProfileName))
			throw new ArgumentException($"Profile name '{DefaultProfileName}' is reserved");

		DefaultProfile = new NetworkProfile(DefaultProfileName);
		PlainProfiles = rawProfiles.Append(DefaultProfile).ToArray();
		Profiles = PlainProfiles.ToDictionary(c => c.Name);
	}


	public NetworkProfile DefaultProfile { get; }

	public IReadOnlyCollection<NetworkProfile> PlainProfiles { get; }

	public IReadOnlyDictionary<string, NetworkProfile> Profiles { get; }
}
