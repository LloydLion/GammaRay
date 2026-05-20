namespace GammaRay.Core.Utils;

public sealed class TimeoutHandle : IDisposable
{
	private readonly SemaphoreSlim _operationLock = new(1);
	private CancellationTokenSource _cts = new();
	private readonly ITimer _timer;


	public TimeoutHandle(TimeProvider timeProvider)
	{
		_timer = timeProvider.CreateTimer(TimerCallback, this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}


	public async ValueTask<TResult> DoAsyncOperationWithTimeout<TArgs, TResult>(
		TimeSpan timeout,
		TArgs args,
		Func<TArgs, CancellationToken, ValueTask<TResult>> task,
		CancellationToken cancellationToken = default
	)
	{
		if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout cannot be zero or negative, for infinite timeout use Timeout.InfiniteTimeSpan");

		await _operationLock.WaitAsync(cancellationToken);

		CancellationTokenRegistration? registration = null;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (timeout == Timeout.InfiniteTimeSpan)
			{
				return await task(args, cancellationToken);
			}


			if (_cts.TryReset() == false)
				_cts = new();
			registration = cancellationToken.Register(_cts.Cancel);

			// _cts.Cancel in timer callback
			_timer.Change(timeout, Timeout.InfiniteTimeSpan);

			try
			{
				return await task(args, _cts.Token);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
			{
				throw new TimeoutException();
			}
		}
		finally
		{
			_timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
			registration?.Unregister();
			_operationLock.Release();
		}
	}

	private static void TimerCallback(object? state)
	{
		if (state is not TimeoutHandle self) return;
		self._cts.Cancel();
	}

	public void Dispose()
	{
		_cts.Dispose();
		_operationLock.Dispose();
		_timer.Dispose();
	}
}
