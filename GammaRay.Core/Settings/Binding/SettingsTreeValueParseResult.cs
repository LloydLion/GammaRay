using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding;

public struct SettingsTreeValueParseResult
{
	private object _result;

	
	private SettingsTreeValueParseResult(object result)
	{
		_result = result;
	}


	public bool Try([NotNullWhen(true)] out object? result, [NotNullWhen(false)] out string? errorMessage)
	{
		if (_result is FailureMessage message)
		{
			result = message;
			errorMessage = message.Message;
			return false;
		}
		
		result = _result;
		errorMessage = null;
		return true;
	}

	public TMatch Match<TMatch>(Func<object, TMatch> positiveBranch, Func<string, TMatch> negativeBranch)
	{
		if (Try(out var result, out var message))
			return positiveBranch(result);
		return negativeBranch(message);
	}
	
	
	public static SettingsTreeValueParseResult Success(object value) => new(value);

	public static SettingsTreeValueParseResult Failure(string message) => new(new FailureMessage(message));


	private record FailureMessage(string Message);
}
