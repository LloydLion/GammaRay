using GammaRay.Core.Settings.Binding.ValueParsers;
using GammaRay.Core.Settings.Tree;

namespace GammaRay.Core.Settings.Binding;

public class SettingsTreeAggregateBinder(IReadOnlyList<SettingsTreeTypeBinder> _binders, IReadOnlyList<ISettingsTreeValueParser> _parsers)
{
	public IReadOnlyList<ISettingsTreeValueParser> Parsers { get; } = _parsers.ToArray();


	public TModel Bind<TModel>(SettingsTreeNode node) => (TModel)Bind(typeof(TModel), node);

	public object Bind(Type type, SettingsTreeNode node)
	{
		var availableBinders = _binders.Where(b => b.CanBind(type, this)).ToArray();

		if (availableBinders.Length == 0)
			throw new InvalidOperationException($"No binder available for type {type.FullName}");

		foreach (var binder in availableBinders)
		{
			if (binder.Bind(node, type, this, out var result))
			{
				return result;
			}
		}

		throw new Exception("TODO: invalid settings handling");
	}
}
