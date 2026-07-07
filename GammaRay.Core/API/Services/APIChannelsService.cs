using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.InternetAccess;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.InternetAccess.Channels.Testing;
using GammaRay.Core.Routing.NetworkProfiles;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace GammaRay.Core.API.Services;

public sealed class APIChannelsService(
	InternetAccessPointProvider _IAPs,
	NetworkProfileProvider _networkProfiles,
	IIAPChannelMonitor _channelMonitor
) : ChannelsService.ChannelsServiceBase
{
	public override async Task QueryIAPChannelStatus(IAPChannelFilter request, IServerStreamWriter<IAPChannelStatusResponse> responseStream, ServerCallContext context)
	{
		IReadOnlyCollection<NetworkProfile> targetNetworks = request.Network == string.Empty ? _networkProfiles.PlainProfiles : [_networkProfiles.Profiles[request.Network]];

		IEnumerable<(InternetAccessPoint IAP, string channelName, IAPChannel channel)> targetChannels = (request.IAP, request.Channel) switch
		{
			("", "") => _IAPs.PlainRemoteInternetAccessPoints.SelectMany(iap => iap.Channels.Select(channel => (iap, channel.Key, channel.Value))),
			(var iap, "") => _IAPs.RemoteInternetAccessPoints[iap].Channels.Select(channel => (_IAPs.RemoteInternetAccessPoints[iap], channel.Key, channel.Value)),
			("", var channel) => throw new InvalidOperationException("Invalid query: channel is set, but IAP is not"),
			(var iap, var channel) => [(_IAPs.RemoteInternetAccessPoints[iap], channel, _IAPs.RemoteInternetAccessPoints[iap].Channels[channel])]
		};

		foreach (var (IAP, channelName, channel) in targetChannels)
		{
			foreach (var network in targetNetworks)
			{
				var status = _channelMonitor.GetStatus(IAP, channel, network);
				var message = new IAPChannelStatusResponse()
				{
					IAP = IAP.Name,
					Channel = channelName,
					Network = network.Name,

					IsAvailable = status.IsAvailable,
					AccessChance = status.AccessChance,
					AverageAccessTime = Duration.FromTimeSpan(status.AverageAccessTime),
					CharacteristicAccessTime = Duration.FromTimeSpan(status.CharacteristicAccessTime)
				};
				await responseStream.WriteAsync(message);
			}
		}
	}
}
