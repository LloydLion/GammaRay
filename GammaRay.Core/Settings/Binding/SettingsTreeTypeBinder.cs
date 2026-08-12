using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding;

public abstract class SettingsTreeTypeBinder
{
	public abstract bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder);

	public abstract bool Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result);
}
