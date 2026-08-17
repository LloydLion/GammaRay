using System.Diagnostics.CodeAnalysis;
using GammaRay.Core.Settings.Tree;

namespace GammaRay.Core.Settings.Binding;

public readonly struct SettingsTreeBindResult
{
	private readonly object _result;

	
	private SettingsTreeBindResult(object result)
	{
		_result = result;
	}
	
	
	public bool IsSuccess => _result is not SettingsTreeBindError;
	
	public bool IsFailed => _result is SettingsTreeBindError;
	
	
	public object Throws(SettingsTree treeForException) =>
		_result is SettingsTreeBindError bindError
			? throw new SettingsTreeBindingException(bindError, treeForException)
			: _result;

	public bool Try(
		[NotNullWhen(false)] out SettingsTreeBindError? error,
		[NotNullWhen(true)] out object? result
	)
	{
		
		error = null;
		result = null;
		if (_result is SettingsTreeBindError bindError)
		{
			error = bindError;
			return false;
		}

		result = _result;
		return true;
	}
	
	public bool TryI(
		[NotNullWhen(true)] out SettingsTreeBindError? error,
		[NotNullWhen(false)] out object? result
	) => !Try(out error, out result);


	public static SettingsTreeBindResult Success(object bindResult) => new(bindResult);

	public static SettingsTreeBindResult Failure(SettingsTreeBindError error) => new(error);

	public static implicit operator SettingsTreeBindResult(SettingsTreeBindError error) => Failure(error);
}
