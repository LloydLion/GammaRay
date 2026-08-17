using System.Diagnostics;
using GammaRay.Core.Settings.Binding.ValueParsers;
using GammaRay.Core.Settings.Tree;

namespace GammaRay.Core.Settings.Binding;

public class SettingsTreeAggregateBinder(IReadOnlyList<SettingsTreeTypeBinder> _binders, IReadOnlyList<ISettingsTreeValueParser> _parsers)
{
	public IReadOnlyList<ISettingsTreeValueParser> Parsers { get; } = _parsers.ToArray();


	public TModel BindTree<TModel>(SettingsTree tree) => (TModel)Bind(typeof(TModel), tree.Root).Throws(tree);

	public SettingsTreeBindResult Bind(Type type, SettingsTreeNode node)
	{
		var availableBinders = _binders.Where(b => b.CanBind(type, this)).ToArray();

		if (availableBinders.Length == 0)
			throw new InvalidOperationException($"No binder available for type {type.FullName}");

		var errors = new SettingsTreeBindErrorCollection();
		
		foreach (var binder in availableBinders)
		{
			var result = binder.Bind(node, type, this);
			if (result.Try(out var error, out _))
				return result;
			
			errors.Add(error);
		}

		if (errors.Or(out var finalError))
			return finalError;
		throw new UnreachableException();
	}
}
