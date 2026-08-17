using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.Binders;

public sealed class ParsePrimitiveBinder : SettingsTreeTypeBinder
{
	public override SettingsTreeBindResult Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		if (node is not SettingsTreeValueNode valueNode || valueNode.Value is null)
			return SettingsTreeBindError.Single("Must be value node with not null value", node);

		return aggregateBinder.Parsers.First(s => s.CanParse(type, aggregateBinder))
			.TryParse(type, valueNode.Value, aggregateBinder)
			.Match(
				SettingsTreeBindResult.Success,
				message => SettingsTreeBindError.Single(message, node)
			);
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return aggregateBinder.Parsers.Any(s => s.CanParse(type, aggregateBinder));
	}

}
