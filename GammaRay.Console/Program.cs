using Dapper;
using GammaRay.Core.Network;
using GammaRay.Core.Persistence;
using GammaRay.Core.Probing;
using GammaRay.Core.Proxy;
using GammaRay.Core.Routing;
using GammaRay.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Runtime;

internal class Program
{
	private static void Main(string[] args)
	{
		var appConfiguration = new ConfigurationBuilder()
			.AddJsonFile("application.json", optional: false)
			.Build();

		InitializeLogger(appConfiguration.GetSection("Logging"));
		var logger = Log.ForContext<Program>();

#if UseWindowsComponents
		logger.Information("Using Windows specific components");
#endif

		GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

		var services = new ServiceCollection()
			.Configure<SettingsProvider.Options>(opt =>
			{
				var path = appConfiguration["Settings:Location"] ?? throw new NullReferenceException("Settings:Location is required");
				opt.SettingsFilePath = Environment.ExpandEnvironmentVariables(path);
			})
			.AddSingleton<SettingsProvider>()
			.AddSingleton<ISettingsProvider>(sp => sp.GetRequiredService<SettingsProvider>())
			.AddSingleton<IDomainCategorizer>(sp => sp.GetRequiredService<ISettingsProvider>())
			.AddSingleton<IRouteGridProvider>(sp => sp.GetRequiredService<ISettingsProvider>())
			.AddSingleton<IConfigurationsProvider>(sp => sp.GetRequiredService<ISettingsProvider>())

			.Configure<SQLiteConnectionFactory.Options>(opt =>
			{
				var path = appConfiguration["Persistence:DatabaseLocation"] ?? throw new NullReferenceException("Persistence:DatabaseLocation is required");
				opt.ConnectionString = "Data Source=" + Environment.ExpandEnvironmentVariables(path);
			})
			.AddSingleton<IDbConnectionFactory, SQLiteConnectionFactory>()

			.AddSingleton<ISiteProber, HttpsSiteProber>(sp => new HttpsSiteProber(sp.GetRequiredService<ISettingsProvider>().GetConfigurations()))
			.AddSingleton<IProbeResultsAnalyzer, SimpleProbeResultsAnalyzer>()

			.Configure<RoutePersistenceDbStorage.Options>(opt =>
			{
				opt.RecordTtl = appConfiguration.GetValue<TimeSpan>("Persistence:RecordTtl");
			})
			.AddSingleton<RoutePersistenceDbStorage>()
			.AddSingleton<IRoutePersistenceStorage>(sp => sp.GetRequiredService<RoutePersistenceDbStorage>())

			.AddSingleton<NetworkProfileDbRepository>(sp =>
			{
				var settings = sp.GetRequiredService<ISettingsProvider>();
				return new(sp.GetRequiredService<IDbConnectionFactory>(), settings.RegisteredProfiles, settings.RegisteredProfiles.First().Name);
			})
			.AddSingleton<INetworkProfileRepository>(s => s.GetRequiredService<NetworkProfileDbRepository>())

			.AddSingleton<IProxyServerRouter, SmartRouter>()

			.Configure<ProxyServer.Options>(opt =>
			{
				opt.MasterClientTimeout = appConfiguration.GetValue<TimeSpan>("Proxy:MasterTimeout");
			})
			.AddSingleton<ProxyServer>()

#if UseWindowsComponents
	.AddSingleton<INetworkIdentifier, GammaRay.Core.Windows.Network.WindowsNetProfileBasedNetworkIdentifier>()
	.AddSingleton<GammaRay.Core.Windows.Management.PowerShellHost>()
#else
			.AddSingleton<INetworkIdentifier, InterfaceBasedNetworkIdentifier>()
#endif

			.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });


		InitializeDatabase(services.GetRequiredService<IDbConnectionFactory>(), logger, args);
		services.GetRequiredService<SettingsProvider>().LoadSettings();
		services.GetRequiredService<NetworkProfileDbRepository>().Initialize();
		services.GetRequiredService<RoutePersistenceDbStorage>().Initialize();

		var settings = services.GetRequiredService<ISettingsProvider>();
		var proxy = services.GetRequiredService<ProxyServer>();

		proxy.Run(settings.Inbounds);



		static void InitializeDatabase(IDbConnectionFactory connectionFactory, ILogger logger, string[] args)
		{
#if DEBUG
			if (args.Contains("--no-db-drop"))
			{
				logger.Information("Due DEBUG build mode, database would be deleted, but '--no-db-drop' flag set");
				return;
			}
			using var connection = connectionFactory.CreateNewConnection();
			logger.Information("Due DEBUG build mode, database will be deleted then created new");
			var tables = connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table';");
			foreach (var table in tables)
				connection.Execute($"DROP TABLE IF EXISTS [{table}];");
#endif
		}

		static void InitializeLogger(IConfiguration configuration)
		{
			if (configuration.GetValue("EnableSelfLog", defaultValue: false))
				Serilog.Debugging.SelfLog.Enable(Console.Error);
			var loggerConfig = new LoggerConfiguration();

			loggerConfig.MinimumLevel.Is(configuration.GetValue("MinimumLevel", LogEventLevel.Information));

			var writeTo = configuration.GetSection("WriteTo");

			var consoleSection = writeTo.GetSection("Console");
			if (consoleSection.Exists())
			{
				var template = consoleSection["Template"] ??
					"""
					[{Timestamp:HH:mm:ss} {Level:u3}][{SourceContext}] {Properties:j} {Message:lj}{NewLine}{Exception}
					""";

				loggerConfig.WriteTo.Console(outputTemplate: template);
			}

			var fileSection = writeTo.GetSection("File");
			if (fileSection.Exists())
			{
				var path = fileSection.GetValue<string>("Path") ?? throw new KeyNotFoundException("No path to log file, if File sink is used 'Path' is required");
				path = Environment.ExpandEnvironmentVariables(path);
				var rollingInterval = fileSection.GetValue<RollingInterval>("RollingInterval");

				loggerConfig.WriteTo.File(
					formatter: new RenderedCompactJsonFormatter(),
					path: path,
					rollingInterval: rollingInterval);
			}

			var seqSection = writeTo.GetSection("Seq");
			if (seqSection.Exists())
			{
				var serverUrl = seqSection["ServerUrl"] ?? throw new KeyNotFoundException("No url to log server, if Seq sink is used 'ServerUrl' is required");
				loggerConfig.WriteTo.Seq(serverUrl);
			}

			Log.Logger = loggerConfig.CreateLogger();
		}
	}
}