using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Utils.Result;

public readonly struct Result<TResult>
{
	private readonly TResult? _result;
	private readonly Exception? _exception;


	[MemberNotNullWhen(true, nameof(_result))]
	[MemberNotNullWhen(false, nameof(_exception))]
	public bool IsSuccess => _exception is null;

	public bool IsFailed => !IsSuccess;


	public Result(TResult result)
	{
		_result = result;
	}

	public Result(Exception exception)
	{
		_exception = exception;
	}


	public TResult Throws() => IsSuccess ? _result : throw _exception;

	public bool Try([NotNullWhen(true)] out TResult? value)
	{
		value = _result;
		return IsSuccess;
	}

	public Result<TSecondResult> Match<TSecondResult, TContext>(TContext ctx, Func<TContext, TResult, TSecondResult> macher)
	{
		if (IsSuccess == false)
			return new(_exception);

		return new(macher(ctx, _result));
	}

	public Result<TSecondResult> Match<TSecondResult, TContext>(TContext ctx, Func<TContext, TResult, TSecondResult> macher,
		Func<TContext, Exception, Exception> exceptionMatcher)
	{
		if (IsSuccess == false)
			return new(exceptionMatcher(ctx, _exception));

		return new(macher(ctx, _result));
	}


	public static implicit operator Result<TResult>(TResult result) => new(result);

	public static implicit operator Result<TResult>(Exception exception) => new(exception);
}
