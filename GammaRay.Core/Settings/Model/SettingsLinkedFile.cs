namespace GammaRay.Core.Settings.Model;

public class SettingsLinkedFile(string fileName, string fileRawContent)
{
	public string FileName { get; } = fileName;

	public string FileRawContent { get; } = fileRawContent;
}
