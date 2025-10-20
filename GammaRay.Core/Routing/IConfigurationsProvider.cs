namespace GammaRay.Core.Routing;

public interface IConfigurationsProvider
{
	public ClientConfigurationQueue GetConfigurationQueue(string queueName);

	public IEnumerable<ClientConfigurationQueue> GetConfigurationQueues();

	public NetClientConfiguration GetConfiguration(string configurationName);

	public IEnumerable<NetClientConfiguration> GetConfigurations();
}
