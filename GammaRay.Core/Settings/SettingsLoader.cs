using System.Diagnostics;
using GammaRay.Core.API;
using GammaRay.Core.Connection.Inbound;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.Network.Profiles;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.Rules;
using GammaRay.Core.Services;
using GammaRay.Core.Settings.Binding;
using GammaRay.Core.Settings.Model;
using GammaRay.Core.Settings.Tree.Loading;
using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GammaRay.Core.Settings;

public class SettingsLoader(IOptions<SettingsLoader.Options> options)
{
	private const string SettingsFileName = "settings.";
	private static readonly Dictionary<string, ISettingsTreeLoader> TreeLoaders = new()
	{
		["YAML"] = new YAMLSettingsTreeLoader()
	};
	
	private readonly Options _options = options.Value;
	
	
	public async ValueTask LoadSettingsAsync(IFileSystemLocator fileSystem, IServiceCollection output)
	{
		var filePath = Path.Combine(_options.SourceFileDirectory, SettingsFileName + _options.FileExtensionFilter);
		var fileContent = await fileSystem.GetFileContentAsync(filePath) ?? throw new FileNotFoundException(filePath);
	
		var treeLoader = TreeLoaders[_options.TreeLoaderType];

		var tree = treeLoader.LoadTree(fileContent);

		var binder = SettingsTreeAggregateBinderSource.Create(fileSystem);
		var modelRoot = binder.BindTree<SettingsModelRoot>(tree);

		CreateProviders(output, modelRoot);
	}

	private static void CreateProviders(IServiceCollection output, SettingsModelRoot modelRoot)
	{
		var networkProfiles = new NetworkProfileProvider(modelRoot);
		output.AddSingleton(networkProfiles);
		output.AddSingleton(new InboundConfigurationProvider(modelRoot));
		output.AddSingleton(new APIConfigurationProvider(modelRoot));
		output.AddSingleton(new CapabilityClassProvider(modelRoot));
		output.AddSingleton(new EndPointCategoriesProvider(modelRoot));

		var internetAccessPoint = new InternetAccessPointProvider(modelRoot, networkProfiles);
		output.AddSingleton(internetAccessPoint);
		var endPointRoutingConfiguration = new EndPointRoutingConfigurationProvider(modelRoot, internetAccessPoint);
		output.AddSingleton(endPointRoutingConfiguration);
		output.AddSingleton(new RoutingRulesProvider(modelRoot, endPointRoutingConfiguration));
	}


	public class Options
	{
		public string SourceFileDirectory { get; init; } = "./settings/";

		public string FileExtensionFilter { get; init; } = "yaml";

		public string TreeLoaderType { get; init; } = "YAML";
	}
}
