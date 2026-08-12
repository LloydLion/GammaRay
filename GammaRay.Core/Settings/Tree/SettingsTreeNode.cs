using System.Collections;

namespace GammaRay.Core.Settings.Tree;

public abstract class SettingsTreeNode : IEnumerable<SettingsTreeNode>
{
	public abstract IEnumerator<SettingsTreeNode> GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
