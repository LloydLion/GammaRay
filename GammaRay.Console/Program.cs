using GammaRay.Core.Network;
using GammaRay.Core.Persistence;
using GammaRay.Core.Probing;
using GammaRay.Core.Proxy;
using GammaRay.Core.Routing;
using GammaRay.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Settings.Configuration;


var appConfiguration = new ConfigurationBuilder()
	.AddJsonFile("application.json", optional: false)
	.Build();

if (appConfiguration.GetValue("Logging:EnableSelfLog", defaultValue: false))
	Serilog.Debugging.SelfLog.Enable(Console.Error);
Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(appConfiguration, new ConfigurationReaderOptions() { SectionName = "Logging" })
	.CreateLogger();

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


var appDbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
	.UseSqlite("Data Source=" + (appConfiguration["Database:Location"] ?? "base.db"))
	.Options
);
InitializeDatabase(appDbContext, Log.ForContext<Program>());

var storage = new RoutePersistenceStorage(Options.Create(new RoutePersistenceStorage.Options()), appDbContext);
storage.Initialize();

var router = new SmartRouter(settingsProvider, settingsProvider, networkProfileRepository, settingsProvider, netId, prober, analyzer, storage);

var proxy = new ProxyServer(Options.Create(new ProxyServer.Options()), router);

proxy.Run(settingsProvider.Inbounds);



static void InitializeDatabase(AppDbContext context, ILogger logger)
{
#if DEBUG
	logger.Information("Due DEBUG build mode, database will be deleted then created new");
	context.Database.EnsureDeleted();
#endif

	logger.Information("Started to initialize database");

	var pendingMigrations = context.Database.GetPendingMigrations().ToArray();
	if (pendingMigrations.Length != 0)
	{
		logger.Information("Database has pending migrations");
		foreach (var migration in pendingMigrations)
			logger.Debug("-- {Migration}", migration);
		context.Database.Migrate();
	}

	logger.Information("Database initialized");
}


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
