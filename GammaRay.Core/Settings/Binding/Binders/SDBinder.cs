using GammaRay.Core.Settings.Model;
using GammaRay.Core.Settings.Tree;
using System.Collections;

namespace GammaRay.Core.Settings.Binding.Binders;

public sealed class SDBinder : SettingsTreeTypeBinder
{
	public override SettingsTreeBindResult Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		var targetType = type.GetGenericArguments()[0];

		if (node is not SettingsTreeMappingNode mappingNode)
			return SettingsTreeBindError.Single("Must be mapping node", node);

		var errors = new SettingsTreeBindErrorCollection();
		
		var dict = (IDictionary)(Activator.CreateInstance(type) ?? throw new InvalidOperationException("Not parameterless constructor available for SD<>"));
		foreach (var (key, valueNode) in mappingNode.Children)
		{
			if (aggregateBinder.Bind(targetType, valueNode).TryI(out var error, out var el))
				errors.Add(error);
			else dict.Add(key, el);
		}

		if (errors.And(out var finalError))
			return finalError;
		return SettingsTreeBindResult.Success(dict);
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SD<>);
	}
}
