namespace GammaRay.Core.Settings.Tree.Loading;

public interface ISettingsTreeLoader
{
	public SettingsTree LoadTree(string sourceSettingsFileContent);
}
