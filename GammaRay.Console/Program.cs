using GammaRay.Core.Network;
using GammaRay.Core.Persistence;
using GammaRay.Core.Probing;
using GammaRay.Core.Proxy;
using GammaRay.Core.Routing;
using GammaRay.Core.Settings;
using Microsoft.Extensions.Options;


var settingsProvider = new SettingsProvider(Options.Create(new SettingsProvider.Options() { SettingsFilePath = "settings.json" }));
settingsProvider.LoadSettings();

#if WindowsRuntime
Console.WriteLine("Using Windows specific components");
#pragma warning disable CA1416
var netId = new GammaRay.Core.Windows.Network.WindowsNetProfileBasedNetworkIdentifier();
#pragma warning restore CA1416
#else
var netId = new GammaRay.Core.Linux.Network.InterfaceBasedNetworkIdentifier();
#endif

var prober = new HttpsSiteProber(settingsProvider.GetConfigurations());
var analyzer = new SimpleProbeResultsAnalyzer();
var networkProfileRepository = new StubNetworkProfileRepository(settingsProvider.RegisteredProfiles.First());

var storage = new RoutePersistenceStorage(Options.Create(new RoutePersistenceStorage.Options()));
storage.PreloadDatabase();

var router = new SmartRouter(settingsProvider, settingsProvider, networkProfileRepository, settingsProvider, netId, prober, analyzer, storage);

var proxy = new ProxyServer(Options.Create(new ProxyServer.Options()), router);

proxy.Run(settingsProvider.Inbounds);



public class StubNetworkProfileRepository : INetworkProfileRepository
{
	public StubNetworkProfileRepository(NetworkProfile profile)
	{
		DefaultProfile = profile;
	}


	public NetworkProfile DefaultProfile { get; }


	public NetworkProfile GetProfileForNetwork(NetworkIdentity network)
	{
		return DefaultProfile;
	}

	public IEnumerable<NetworkIdentity> ListProfileNetworks(NetworkProfile profile)
	{
		return [];
	}
}
