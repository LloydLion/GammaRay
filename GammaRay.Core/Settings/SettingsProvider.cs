using GammaRay.Core.Routing;
using GammaRay.Core.Settings.Entities;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace GammaRay.Core.Settings;

public sealed class SettingsProvider(IOptions<SettingsProvider.Options> options) : ISettingsProvider
{
	private readonly Options _options = options.Value;
	private LoadedSettings? _settings;


	private LoadedSettings Settings => _settings ?? throw new InvalidOperationException("Load settings before use any other method");

	public IReadOnlyDictionary<string, InboundSettings> Inbounds => Settings.RawObject.Inbounds;

	public IReadOnlyCollection<NetworkProfile> RegisteredProfiles => Settings.Profiles;

	public DomainCategory DefaultCategory => Settings.DefaultCategory;


	public DomainCategory GetCategoryForDomain(string domainName)
	{
		foreach (var (patterns, category) in Settings.DomainTable)
		{
			if (patterns.Any(s => s.IsMatch(domainName)))
				return category;
		}
		return Settings.DefaultCategory;
	}

	public NetClientConfiguration GetConfiguration(string configurationName)
	{
		return Settings.Configurations[configurationName];
	}

	public ClientConfigurationQueue GetConfigurationQueue(string queueName)
	{
		return Settings.Queues[queueName];
	}

	public string GetConfigurationQueueName(NetworkProfile profile, DomainCategory category)
	{
		return Settings.RouteGrid[(category.Name, profile.Name)];
	}

	public IEnumerable<ClientConfigurationQueue> GetConfigurationQueues()
	{
		return Settings.Queues.Values;
	}

	public IEnumerable<NetClientConfiguration> GetConfigurations()
	{
		return Settings.Configurations.Values;
	}

	public void LoadSettings()
	{
		try
		{
			ApplicationSettings rawSettings;
			using (var file = File.OpenRead(_options.SettingsFilePath))
			{
				rawSettings = JsonSerializer.Deserialize<ApplicationSettings>(file) ?? throw new FormatException("Empty");
			}

			var profiles = rawSettings.NetworkProfiles.Select(s => new NetworkProfile(s.Key)).ToDictionary(s => s.Name);
			var (defaultCategory, domainTable) = LoadCategories(rawSettings);
			var configurations = LoadConfigurations(rawSettings).ToDictionary(s => s.Name);
			var queues = rawSettings.PriorityQueues.Select(s => new ClientConfigurationQueue(s.Key, s.Value.Select(c => configurations[c]).ToArray())).ToDictionary(s => s.Name);
			var routeGrid = LoadRouteGrid(rawSettings);


			_settings = new LoadedSettings(
				rawSettings,
				profiles.Values,
				defaultCategory,
				domainTable,
				configurations,
				queues,
				routeGrid
			);
		}
		catch (Exception ex)
		{
			throw new SettingsLoadException(ex, $"File={_options.SettingsFilePath}");
		}
	}

	private static Dictionary<(string Category, string Profile), string> LoadRouteGrid(ApplicationSettings rawSettings)
	{
		//TODO: add validation
		string[] profilesInOrder = rawSettings.RouteGrid.ProfilesOrder;

		var routeGrid = new Dictionary<(string Category, string Profile), string>();
		foreach (var categoryEntry in rawSettings.RouteGrid.Grid)
		{
			var categoryName = categoryEntry.Key;
			int index = 0;
			foreach (var queueName in categoryEntry.Value)
			{
				var profileName = profilesInOrder[index];
				routeGrid.Add((categoryName, profileName), queueName);
				index++;
			}
		}
		return routeGrid;
	}

	private static NetClientConfiguration[] LoadConfigurations(ApplicationSettings rawSettings)
	{
		return rawSettings.Configurations.Select(s => new NetClientConfiguration(s.Key)
		{
			ProxyServer = s.Value.ProxyServer is null ? null : IPEndPoint.Parse(s.Value.ProxyServer),
			MaxRequestCount = s.Value.MaxRequestCount,
			RequestCount = s.Value.RequestCount,
			RequestInternal = TimeSpan.FromMilliseconds(s.Value.RequestInternalMs),
			Timeout = TimeSpan.FromMilliseconds(s.Value.TimeoutMs)
		}).ToArray();
	}

	private (DomainCategory DefaultCategory, List<(DomainPattern[] Patterns, DomainCategory Category)> DomainTable) LoadCategories(ApplicationSettings rawSettings)
	{
		var categories = rawSettings.Categories.ToDictionary(s => s.Key, s => (RawObject: s.Value, CategoryObject: new DomainCategory(s.Key)));
		DomainCategory? defaultCategory = null;
		var domainTable = new List<(DomainPattern[] Patterns, DomainCategory Category)>();
		foreach (var (rawObject, categoryObject) in categories.Values)
		{
			if (rawObject.IsDefault)
			{
				if (defaultCategory is not null)
					throw new Exception($"Two (or more) default domain categories: '{defaultCategory.Name}', '{categoryObject.Name}'");
				defaultCategory = categoryObject;
				continue;
			}

			if (rawObject.List is null)
				throw new Exception($"List of domains is required property for non-default category (or just remove it): '{categoryObject.Name}'");

			var patterns = LoadPatterns(rawObject.List);
			domainTable.Add((patterns, categoryObject));
		}

		if (defaultCategory is null)
			throw new Exception("No default domain category");

		return (defaultCategory, domainTable);
	}

	private DomainPattern[] LoadPatterns(string fileName)
	{
		fileName = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_options.SettingsFilePath))!, fileName);
		var lines = File.ReadAllLines(fileName);
		return lines.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => new DomainPattern(s.Trim())).ToArray();
	}


	private record LoadedSettings(
		ApplicationSettings RawObject,
		IReadOnlyCollection<NetworkProfile> Profiles,
		DomainCategory DefaultCategory,
		List<(DomainPattern[] Patterns, DomainCategory Category)> DomainTable,
		Dictionary<string, NetClientConfiguration> Configurations,
		Dictionary<string, ClientConfigurationQueue> Queues,
		Dictionary<(string Category, string Profile), string> RouteGrid
	);

	private class DomainPattern(string rawPattern)
	{
		private readonly string[] _parts = rawPattern.Split('.');


		public bool IsMatch(string domainName)
		{
			var domainParts = domainName.Split('.');
			if (domainName.Length < _parts.Length)
				return false;

			for (int i = 1; i <= _parts.Length; i++)
			{
				var domainPart = domainParts[^i];
				var patternPart = _parts[^i];
				if (domainPart != patternPart)
					return false;
			}

			return true;
		}
	}

	public class Options
	{
		public required string SettingsFilePath { get; init; }
	}
}
