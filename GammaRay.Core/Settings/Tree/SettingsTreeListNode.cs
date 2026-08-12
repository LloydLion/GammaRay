namespace GammaRay.Core.Settings.Tree;

public sealed class SettingsTreeListNode(IReadOnlyList<SettingsTreeNode> children) : SettingsTreeNode
{
	public IReadOnlyList<SettingsTreeNode> Children { get; } = children;


	public override IEnumerator<SettingsTreeNode> GetEnumerator() => Children.GetEnumerator();
}
