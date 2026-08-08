using GammaRay.Core.API.Services.Proto;
using Grpc.Core;

namespace GammaRay.Core.API.Client;

internal sealed class AggregateServiceClient(Channel channel)
{
	public BasicService.BasicServiceClient Basic { get; } = new BasicService.BasicServiceClient(channel);

	public ChannelsService.ChannelsServiceClient Channels { get; } = new ChannelsService.ChannelsServiceClient(channel);

	public ControlService.ControlServiceClient Control { get; } = new ControlService.ControlServiceClient(channel);

	public MonitoringService.MonitoringServiceClient Monitoring { get; } = new MonitoringService.MonitoringServiceClient(channel);

	public ServicesService.ServicesServiceClient Services { get; } = new ServicesService.ServicesServiceClient(channel);

	public FileSystemService.FileSystemServiceClient FileSystem { get; } = new FileSystemService.FileSystemServiceClient(channel);

	public NetworkService.NetworkServiceClient Network { get; } = new NetworkService.NetworkServiceClient(channel);
}
