namespace GammaRay.Core.Settings;

public class SettingsLoadException : Exception
{
	public SettingsLoadException(Exception innerException, string? message = null) : base(FormMessage(message), innerException)
	{

	}


	private static string FormMessage(string? message)
	{
		if (message is null)
			return "Failed to load settings";
		return "Failed to load settings: " + message;
	}
}
