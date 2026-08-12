using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.Binders;

public sealed class ArrayBinder : SettingsTreeTypeBinder
{
	public override bool Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result)
	{
		var elementType = type.GetElementType() ?? throw new ArgumentException("Type must be array", nameof(type));

		result = null;
		if (node is not SettingsTreeListNode listNode)
			return false;

		var size = listNode.Children.Count;
		var array = Array.CreateInstance(elementType, size);
		for (int i = 0; i < size; i++)
		{
			var el = aggregateBinder.Bind(elementType, listNode.Children[i]);
			array.SetValue(el, i);
		}

		result = array;
		return true;
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type.IsArray;
	}
}
