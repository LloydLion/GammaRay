namespace GammaRay.Core.API;

public sealed class APIEndpointInformation(string protocol, string configurationString)
{
	public string Protocol { get; } = protocol;

	public string ConfigurationString { get; } = configurationString;


	public override string ToString()
	{
		return $"APIEndpoint {{{Protocol}:{ConfigurationString}}}";
	}
}
