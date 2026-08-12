using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GammaRay.Core.Settings.Binding.Binders;

public sealed class CommonObjectBinder : SettingsTreeTypeBinder
{
	public override bool Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result)
	{
		result = null;
		var defaultCtor = type.GetConstructor([]) ?? throw new ArgumentException("Type does not have a parameterless constructor", nameof(type));

		if (node is not SettingsTreeMappingNode mappingNode)
			return false;

		result = defaultCtor.Invoke([]);

		foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
		{		
			var isRequired = field.GetCustomAttribute<RequiredMemberAttribute>() is not null;

			if (mappingNode.Children.TryGetValue(field.Name, out var fieldValueNode) == false)
			{
				if (isRequired) return false;
				else continue;
			}

			var fieldValue = aggregateBinder.Bind(field.FieldType, fieldValueNode);

			field.SetValue(result, fieldValue);
		}

		return true;
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type.IsValueType == false && type.IsArray == false && type.IsAbstract == false && type.GetConstructor([]) is not null;
	}
}
