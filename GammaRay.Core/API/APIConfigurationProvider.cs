using GammaRay.Core.Settings;

namespace GammaRay.Core.API;

public sealed class APIConfigurationProvider(IRawSettingsProvider<APIConfiguration> _configurationProvider)
{
	public APIConfiguration Configuration { get; } = _configurationProvider.Get();
}
