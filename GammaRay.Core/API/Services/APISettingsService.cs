using GammaRay.Core.API.Services.Proto;
using GammaRay.Core.Settings;
using Grpc.Core;

namespace GammaRay.Core.API.Services;

public sealed class APISettingsService(SettingsFileHolder _settingsFile) : SettingsService.SettingsServiceBase
{
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
}
