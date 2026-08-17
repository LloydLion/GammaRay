using System.Collections;
using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using GammaRay.Core.Utils.Result;

namespace GammaRay.Core.Settings.Binding.Binders;

public sealed class CommonObjectBinder : SettingsTreeTypeBinder
{
	public override SettingsTreeBindResult Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		var defaultCtor = type.GetConstructor([]) ?? throw new ArgumentException("Type does not have a parameterless constructor", nameof(type));

		if (node is not SettingsTreeMappingNode mappingNode)
			return SettingsTreeBindError.Single("Must be mapping node", node);

		var result = defaultCtor.Invoke([]);

		var errors = new SettingsTreeBindErrorCollection();

		foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
		{		
			var isRequired = field.GetCustomAttribute<RequiredMemberAttribute>() is not null;

			if (mappingNode.Children.TryGetValue(field.Name, out var fieldValueNode) == false)
			{
				if (isRequired)
					errors.Add(SettingsTreeBindError.Single($"Required field '{field.Name}' missing", node));
				continue;
			}

			var fieldValueResult = aggregateBinder.Bind(field.FieldType, fieldValueNode);

			if (fieldValueResult.TryI(out var error, out var fieldValue))
				errors.Add(error);
			else field.SetValue(result, fieldValue);
		}

		if (errors.And(out var finalError))
			return finalError;
		
		return SettingsTreeBindResult.Success(result);
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return 
			type is { IsValueType: false, IsArray: false, IsAbstract: false } &&
			type.GetConstructor([]) is not null &&
			type.IsAssignableTo(typeof(IEnumerable)) == false;
	}
}
