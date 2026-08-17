using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.Binders;

public sealed class ArrayBinder : SettingsTreeTypeBinder
{
	public override SettingsTreeBindResult Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		var elementType = type.GetElementType() ?? throw new ArgumentException("Type must be array", nameof(type));

		if (node is not SettingsTreeListNode listNode)
			return SettingsTreeBindError.Single("Must be list node", node);

		var errors = new SettingsTreeBindErrorCollection();
		
		var size = listNode.Children.Count;
		var array = Array.CreateInstance(elementType, size);
		for (int i = 0; i < size; i++)
		{
			var elementBindResult = aggregateBinder.Bind(elementType, listNode.Children[i]);
			if (elementBindResult.TryI(out var error, out var el))
				errors.Add(error);
			else array.SetValue(el, i);
		}

		if (errors.And(out var finalError))
			return finalError;
		return SettingsTreeBindResult.Success(array);
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type.IsArray;
	}
}
