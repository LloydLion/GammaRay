using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Utils.FileSystem;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLEndPointCategoryRawProvider(IFileSystemLocator _fileSystemLocator) : IRawSettingsProvider<IReadOnlyCollection<EndPointCategory>>
{
	private IReadOnlyCollection<EndPointCategory>? _categories;

	public bool IsInitialized => _categories is not null;


	public IReadOnlyCollection<EndPointCategory> Get()
	{
		return _categories ?? throw new InvalidOperationException("Not initialized");
	}

	public void Initialize(YAMLConfigurationLoader YAMLLoader)
	{
		_categories = LoadEndPointCategories(YAMLLoader.GetFragment<YamlMappingNode>("endPointCategories")).Values;
	}

	private Dictionary<string, EndPointCategory> LoadEndPointCategories(YamlMappingNode node) =>
		node.ScalarChildrenMap.Select(kv =>
		{
			var name = kv.Key;
			var node = (YamlMappingNode)kv.Value;

			var patterns = new List<EndPointPattern>();

			if (node.TryGet<YamlScalarNode>("list", out var listNode))
			{
				var path = listNode.Bind<string>();

				using var stream = _fileSystemLocator!.Open(path);
				using var reader = new StreamReader(stream);
				while (true)
				{
					var line = reader.ReadLine();
					if (line is null)
						break;

					if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith("//"))
						continue;

					patterns.Add(EndPointPattern.Parse(line));
				}
			}
			return new EndPointCategory(name, patterns);
		}).ToDictionary(s => s.Name);
}
