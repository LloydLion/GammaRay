namespace GammaRay.Core.Routing;

public class ClientConfigurationQueue(string name, IEnumerable<NetClientConfiguration> configurations)
{
	public string Name { get; } = name;

	public IEnumerable<NetClientConfiguration> OrderedConfigurations { get; } = configurations;
}
