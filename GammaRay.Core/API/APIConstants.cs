namespace GammaRay.Core.API;

public static class APIConstants
{
	public const byte APIVersion = 0;
	public const int MaxMessageSize = 1024 * 16 - 1;
	public const int MessageInBufferOffset = 1;
	public const int AllocationBufferSize = MaxMessageSize + MessageInBufferOffset;


	public enum RequestType : byte
	{
		GetAPIVersion = 0, // Never change

		GetCurrentSettingsFile = 11,
		UploadNewSettingsFile = 12,

		ControlMonitoring = 21,

		ReloadApplication = 31
	}

	public enum ResponseCode : byte
	{
		Success = 0,
		UnknownRequestType = 1,
		ClientSideError = 2,
		ServerSideError = 3,
	}

	public enum EventType : byte
	{
		MonitoringNewContext = 1,
		MonitoringCloseContext = 2,
		MonitoringNewReport = 3,
		MonitoringSetReportProperty = 4,
		MonitoringFinishReport = 5,
	}

	public enum MonitoringMode : byte
	{
		Disabled = 0,
		Enabled = 1,
		EnabledWithReportProperties = 2,
	}

	public enum ServerMessageType : byte
	{
		Response = 1,
		Event = 2
	}
}
