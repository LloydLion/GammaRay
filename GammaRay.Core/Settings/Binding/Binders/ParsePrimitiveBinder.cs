using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.Binders;

public sealed class ParsePrimitiveBinder() : SettingsTreeTypeBinder
{
	public override bool Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result)
	{
		result = null;
		if (node is not SettingsTreeValueNode valueNode || valueNode.Value is null)
			return false;

		return aggregateBinder.Parsers.First(s => s.CanParse(type, aggregateBinder)).TryParse(type, valueNode.Value, aggregateBinder, out result);
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return aggregateBinder.Parsers.Any(s => s.CanParse(type, aggregateBinder));
	}

}
