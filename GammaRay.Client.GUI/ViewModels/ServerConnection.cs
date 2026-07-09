using GammaRay.Core.API.Client;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using GammaRay.Core.Network.Profiles;

namespace GammaRay.Client.GUI.ViewModels;

public sealed class ServerConnection(
	GammaRayAPIClient apiClient,
	MonitoringSerializerOptionsSource serializerOptionsSource,
	MonitoringConnectionTracker connectionTracker,
	MonitoringSystem monitoringSystem,
	APIMonitoringEventListener eventListener,
	ServerStateObserver observer
,
	NetworkProfileProvider networkProfiles)
{
	public GammaRayAPIClient APIClient { get; } = apiClient;

	public MonitoringSerializerOptionsSource SerializerOptionsSource { get; } = serializerOptionsSource;

	public MonitoringConnectionTracker ConnectionTracker { get; } = connectionTracker;

	public MonitoringSystem MonitoringSystem { get; } = monitoringSystem;

	public APIMonitoringEventListener EventListener { get; } = eventListener;

	public ServerStateObserver Observer { get; } = observer;

	public NetworkProfileProvider NetworkProfiles { get; } = networkProfiles;
}
