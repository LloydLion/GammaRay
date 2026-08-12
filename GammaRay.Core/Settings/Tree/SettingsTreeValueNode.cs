namespace GammaRay.Core.Settings.Tree;

public sealed class SettingsTreeValueNode(string? value) : SettingsTreeNode
{
	public string? Value { get; } = value;


	public override IEnumerator<SettingsTreeNode> GetEnumerator() => Enumerable.Empty<SettingsTreeNode>().GetEnumerator();
}
