namespace GammaRay.Core.Routing;

public class ClientConfigurationQueue(string name, IReadOnlyList<NetClientConfiguration> configurations)
{
	public string Name { get; } = name;

	public IReadOnlyList<NetClientConfiguration> OrderedConfigurations { get; } = configurations;
}
