using System.CommandLine;
using GammaRay.Core.Host;
using GammaRay.Core.Settings;
using GammaRay.Core.Utils.FileSystem;
using GammaRay.Server.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;


var rootCommand = new RootCommand("GammaRay server console application");
var enableCommandMonitoring = new Option<bool>("--enable-console-monitoring");
rootCommand.SetAction(cmdOptions =>
{
	var applicationControl = new ApplicationControl(async (applicationControl, cancel) =>
	{
		await GammaRayServerBuilder
			.Create(applicationControl)
			.ControlConsoleMonitoring(cmdOptions.GetValue(enableCommandMonitoring) ? true : null)
			.Configure(builder => loadSettings(builder.Services, builder.FileSystem))
			.BuildAndRunAsync(cancel);
	});

	applicationControl.MainLoop();
});

rootCommand.Parse(args).Invoke();


static void loadSettings(IServiceCollection services, IFileSystemLocator fileSystemLocator)
{
	AsyncContext.Run(async () =>
	{
		var loader = new SettingsLoader(Options.Create(new SettingsLoader.Options()));
		await loader.LoadSettingsAsync(fileSystemLocator, services);
	});
}

