using GammaRay.Core.Settings.Model;

namespace GammaRay.Core.API;

public sealed class APIConfigurationProvider(SettingsModelRoot modelRoot)
{
	public APIConfiguration Configuration { get; } = 
		new(
			modelRoot.API?.EndPoints
				.Select(modelEndpoint => new APIEndpointInformation(modelEndpoint.BindAddress, modelEndpoint.Port))
				.ToArray() ?? []
		);
}
