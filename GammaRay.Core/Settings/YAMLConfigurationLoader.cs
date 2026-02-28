using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLConfigurationLoader
{
	private YamlMappingNode? _root;


	public void LoadSettings(TextReader fileContent)
	{
		var stream = new YamlStream();
		stream.Load(fileContent);
		var document = stream.Documents[0];
		_root = document.RootNode.AsMapping();
	}

	public YamlMappingNode GetFragment(string key) =>
		_root?.ExceptMappingChild(key) ?? throw new InvalidOperationException("Configuration not loaded");
}
