using GammaRay.Core.Settings.Model;
using GammaRay.Core.Settings.Tree;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.Binders;

public sealed class SDBinder : SettingsTreeTypeBinder
{
	public override bool Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result)
	{
		result = null;
		var targetType = type.GetGenericArguments()[0];

		if (node is not SettingsTreeMappingNode mappingNode)
			return false;

		var dict = (IDictionary)(Activator.CreateInstance(type) ?? throw new InvalidOperationException("Not parameterless constructor available for SD<>"));
		foreach (var (key, valueNode) in mappingNode.Children)
			dict.Add(key, aggregateBinder.Bind(targetType, valueNode));

		result = dict;
		return true;
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SD<>);
	}
}
