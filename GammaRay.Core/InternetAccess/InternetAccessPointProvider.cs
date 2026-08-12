using System.Collections.Immutable;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Network.Profiles;
using GammaRay.Core.Settings.Model;

namespace GammaRay.Core.InternetAccess;

public sealed class InternetAccessPointProvider
{
	public const string LocalIAPPrefix = "local:";
	public const string LocalIAPChannelName = "local";
	public const string LocalIAPChannelDriverName = "local";


	public InternetAccessPointProvider(SettingsModelRoot modelRoot, NetworkProfileProvider networkProfiles)
	{
		var IAPs = modelRoot.InternetAccessPoints.Select(cm => new InternetAccessPoint(cm.Key, cm.Value.Channels.Select(cm =>
		{
			var channel = new IAPChannel(cm.Value.Protocol, cm.Value.EndPoint)
			{
				AvailableInNetwork = cm.Value.AvailableInNetwork?.Select(n => networkProfiles.Profiles[n]).ToArray() 
					?? networkProfiles.PlainProfiles.ToArray(),
				Parameters = cm.Value.Parameters ?? (IReadOnlyDictionary<string, string>)ImmutableDictionary<string, string>.Empty,
				Tags = cm.Value.Tags ?? []
			};
			return KeyValuePair.Create(cm.Key, channel);
		}).ToDictionary())).ToArray();

		var invalidIAP = IAPs.FirstOrDefault(s => s.Name.StartsWith(LocalIAPPrefix));
		if (invalidIAP is not null)
			throw new ArgumentException($"'{invalidIAP.Name}' is invalid: '{LocalIAPPrefix}' is reserved prefix");

		RemoteInternetAccessPoints = IAPs.ToDictionary(s => s.Name);
		PlainRemoteInternetAccessPoints = IAPs.ToArray();

		LocalInternetAccessPointsByProfile = networkProfiles.PlainProfiles.ToDictionary(s => s, createLocalIAP);
		LocalInternetAccessPointsByName = LocalInternetAccessPointsByProfile.ToDictionary(kv => kv.Key.Name, kv => kv.Value);
		PlainLocalInternetAccessPoints = LocalInternetAccessPointsByProfile.Values.ToArray();

		PlainInternetAccessPoints = PlainRemoteInternetAccessPoints.Concat(PlainLocalInternetAccessPoints).ToArray();
		InternetAccessPoints = PlainInternetAccessPoints.ToDictionary(s => s.Name);


		static InternetAccessPoint createLocalIAP(NetworkProfile profile)
		{
			var localChannel = new IAPChannel(LocalIAPChannelDriverName, default) { AvailableInNetwork = [profile] };
			var channels = new Dictionary<string, IAPChannel>() { [LocalIAPChannelName] = localChannel };
			return new InternetAccessPoint($"{LocalIAPPrefix}{profile.Name}", channels);
		}
	}


	public IReadOnlyCollection<InternetAccessPoint> PlainInternetAccessPoints { get; }

	public IReadOnlyDictionary<string, InternetAccessPoint> InternetAccessPoints { get; }

	public IReadOnlyCollection<InternetAccessPoint> PlainLocalInternetAccessPoints { get; }

	public IReadOnlyDictionary<string, InternetAccessPoint> LocalInternetAccessPointsByName { get; }

	public IReadOnlyDictionary<NetworkProfile, InternetAccessPoint> LocalInternetAccessPointsByProfile { get; }

	public IReadOnlyCollection<InternetAccessPoint> PlainRemoteInternetAccessPoints { get; }

	public IReadOnlyDictionary<string, InternetAccessPoint> RemoteInternetAccessPoints { get; }
}
