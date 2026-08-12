using GammaRay.Core.Settings.Model;

namespace GammaRay.Core.Settings.Tree;

public sealed class SettingsTreeMappingNode(IReadOnlyDictionary<string, SettingsTreeNode> children) : SettingsTreeNode
{
	public IReadOnlyDictionary<string, SettingsTreeNode> Children { get; } = new SD<SettingsTreeNode>(children);


	public override IEnumerator<SettingsTreeNode> GetEnumerator() => Children.Values.GetEnumerator();
}
