using System.Collections.Frozen;

namespace GammaRay.Core.Settings.Tree;

public sealed class SettingsTree(SettingsTreeNode root)
{
	public SettingsTreeNode Root { get; } = root;

	public IReadOnlyDictionary<SettingsTreeNode, SettingsTreeNodeInTreeProperties> InTreeProperties { get; } = BuildInTreeProperties(root);


	private static FrozenDictionary<SettingsTreeNode, SettingsTreeNodeInTreeProperties> BuildInTreeProperties(SettingsTreeNode root)
	{
		var result = new Dictionary<SettingsTreeNode, SettingsTreeNodeInTreeProperties>();
		var rootNodeProperties = new SettingsTreeNodeInTreeProperties(root, "$");
		result.Add(root, rootNodeProperties);
		visit(root, rootNodeProperties);
		return result.ToFrozenDictionary();

		
		void visit(SettingsTreeNode node, SettingsTreeNodeInTreeProperties properties)
		{
			switch (node)
			{
				case SettingsTreeMappingNode mappingNode:
					foreach (var (key, child) in mappingNode.Children)
					{
						var childProperties = new SettingsTreeNodeInTreeProperties(node, $"{properties.Path}.{key}");
						result.Add(child, childProperties);
						visit(child, childProperties);
					}
					break;
				case SettingsTreeListNode listNode:
					for (int i = 0; i < listNode.Children.Count; i++)
					{
						var child = listNode.Children[i];
						var childProperties = new SettingsTreeNodeInTreeProperties(node, $"{properties.Path}[{i}]");
						result.Add(child, childProperties);
						visit(child, childProperties);
					}
					break;
			}
		}
	}
}
