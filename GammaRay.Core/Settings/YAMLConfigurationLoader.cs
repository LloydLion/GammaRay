using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings;

public class YAMLConfigurationLoader
{
	private YamlMappingNode? _root;


	public void LoadSettings(string fileContent)
	{
		var stream = new YamlStream();
		stream.Load(new StringReader(fileContent));
		var document = stream.Documents[0];
		_root = document.RootNode.AsMapping();
	}

	public TChild GetFragment<TChild>(string key) where TChild : YamlNode =>
		_root?.ExceptChild<TChild>(key) ?? throw new InvalidOperationException("Configuration not loaded");
}
