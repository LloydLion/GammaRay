using GammaRay.Core.Linux.Network;
using GammaRay.Core.Network;
using GammaRay.Core.Persistence;
using GammaRay.Core.Probing;
using GammaRay.Core.Proxy;
using GammaRay.Core.Routing;
using GammaRay.Core.Settings;
//using GammaRay.Core.Windows.Network;
using Microsoft.Extensions.Options;
using System.Net;


var settingsProvider = new SettingsProvider(Options.Create(new SettingsProvider.Options() { SettingsFilePath = "settings.json" }));
settingsProvider.LoadSettings();

var netId = new LinuxNetProfileBasedNetworkIdentifier();

var prober = new HttpsSiteProber(settingsProvider.GetConfigurations());
var analyzer = new SimpleProbeResultsAnalyzer();
var networkProfileRepository = new StubNetworkProfileRepository(settingsProvider.RegisteredProfiles.First());

var storage = new RoutePersistenceStorage();
storage.PreloadDatabase();

var router = new SmartRouter(settingsProvider, settingsProvider, networkProfileRepository, settingsProvider, netId, prober, analyzer, storage);

var proxy = new ProxyServer(Options.Create(new ProxyServer.Options
{
	ListenEndPoint = IPEndPoint.Parse(settingsProvider.Inbounds.First().Value.EndPoint)
}), router);

proxy.Run();





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
