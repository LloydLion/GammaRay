using Grpc.Core;
using GammaRay.Core.Host;
using GammaRay.Core.Settings;
using GammaRay.Core.API.Proto;
using Channel = System.Threading.Channels.Channel;

namespace GammaRay.Core.API;

public sealed class GammaRayApiService(
	APIBasedMonitoringSystem monitoringSystem,
	ApplicationControl applicationControl,
	SettingsFileHolder settingsFile
) : GammaRayService.GammaRayServiceBase
{
	private readonly APIBasedMonitoringSystem _monitoringSystem = monitoringSystem;
	private readonly ApplicationControl _applicationControl = applicationControl;
	private readonly SettingsFileHolder _settingsFile = settingsFile;

	public override Task<VersionResponse> GetAPIVersion(Empty request, ServerCallContext context)
	{
		return Task.FromResult(new VersionResponse { Version = APIConstants.APIVersion });
	}

	public override async Task<SettingsFileResponse> GetCurrentSettingsFile(Empty request, ServerCallContext context)
	{
		using var settingsFile = _settingsFile.ReadConfigurationFile();
		var content = await settingsFile.ReadToEndAsync();
		return new SettingsFileResponse { Content = content };
	}

	public override async Task<Empty> UploadNewSettingsFile(SettingsFileRequest request, ServerCallContext context)
	{
		using var settingsFile = _settingsFile.WriteConfigurationFile();
		await settingsFile.WriteAsync(request.Content);
		return new Empty();
	}

	public override Task<Empty> ReloadApplication(Empty request, ServerCallContext context)
	{
		_applicationControl.Restart();
		return Task.FromResult(new Empty());
	}

	public override async Task SubscribeEvents(Empty request, IServerStreamWriter<MonitoringEvent> responseStream, ServerCallContext context)
	{
		var channel = Channel.CreateUnbounded<MonitoringEvent>();

		using var subscription = _monitoringSystem.Subscribe((monitoringEvent) => channel.Writer.TryWrite(monitoringEvent));


		using (var enumerator = _monitoringSystem.GetPendingEvents().GetEnumerator())
		{
			if (enumerator.MoveNext())
				while (true)
				{
					var evt = enumerator.Current;
					var hasNext = enumerator.MoveNext();

					evt.Type = hasNext ? MonitoringEvent.Types.SyncronizationType.Buffered : MonitoringEvent.Types.SyncronizationType.LastBuffered;
					await responseStream.WriteAsync(evt);

					if (hasNext == false)
						break;
				}
		}

		try
		{
			await foreach (var evt in channel.Reader.ReadAllAsync(context.CancellationToken))
			{
				await responseStream.WriteAsync(evt);
			}
		}
		catch (OperationCanceledException) { }
	}
}
