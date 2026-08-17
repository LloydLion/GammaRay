using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding;

public struct SettingsTreeBindErrorCollection
{
	private List<SettingsTreeBindError>? _errors;
	
	
	public void Add(SettingsTreeBindError error)
	{
		_errors ??= [];
		_errors.Add(error);
	}
	
	public bool Or([NotNullWhen(true)] out SettingsTreeBindError? error)
	{
		if (_errors is not null)
		{
			error = SettingsTreeBindError.Or(_errors);
			return true;
		}
		
		error = null;
		return false;
	}
	
	public bool And([NotNullWhen(true)] out SettingsTreeBindError? error)
	{
		if (_errors is not null)
		{
			error = SettingsTreeBindError.And(_errors);
			return true;
		}
		
		error = null;
		return false;
	}
}
