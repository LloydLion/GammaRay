using GammaRay.Core.InternetAccess;
using GammaRay.Core.Routing;
using GammaRay.Core.Routing.Categorization;
using GammaRay.Core.Routing.NetworkProfiles;
using GammaRay.Core.Services;
using System.Text.Json;

namespace GammaRay.Core.Monitoring.Converters;

public class MonitoringSerializerOptionsSource
{
	public MonitoringSerializerOptionsSource(
		CapabilityClassProvider? _capabilityClassProvider = null,
		EndPointCategoriesProvider? _endPointCategoryProvider = null,
		EndPointRoutingConfigurationProvider? _endPointRoutingConfigurationProvider = null,
		InternetAccessPointProvider? _internetAccessPointProvider = null,
		NetworkProfileProvider? _networkProfileProvider = null
	)
	{
		JsonOptions = new JsonSerializerOptions();
		JsonOptions.Converters.Add(new CapabilityClassConverter(_capabilityClassProvider));
		JsonOptions.Converters.Add(new CapabilityConverter());
		JsonOptions.Converters.Add(new EndPointCategoryConverter(_endPointCategoryProvider));
		JsonOptions.Converters.Add(new EndPointRoutingConfigurationConverter(_endPointRoutingConfigurationProvider));
		JsonOptions.Converters.Add(new InternetAccessPointConverter(_internetAccessPointProvider));
		JsonOptions.Converters.Add(new ServiceIAPStatusConverter());
		JsonOptions.Converters.Add(new ProbeResultConverter());
		JsonOptions.Converters.Add(new IPEndPointConverter());
		JsonOptions.Converters.Add(new IAPChannelStatusConverter());
		JsonOptions.Converters.Add(new ServiceStatusTableConverter(_internetAccessPointProvider));
		JsonOptions.Converters.Add(new NetworkProfileConverter(_networkProfileProvider));
		JsonOptions.Converters.Add(new ServiceConverter());
	}


	public JsonSerializerOptions JsonOptions { get; }
}
