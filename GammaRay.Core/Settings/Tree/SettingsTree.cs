namespace GammaRay.Core.Settings.Tree;

public sealed class SettingsTree(SettingsTreeNode root)
{
	public SettingsTreeNode Root { get; } = root;
}
