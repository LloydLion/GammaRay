namespace GammaRay.Core.API;

public sealed class APIConfiguration(IReadOnlyCollection<APIEndpointInformation> endPoints)
{
	public IReadOnlyCollection<APIEndpointInformation> EndPoints { get; } = endPoints;
}
