using GammaRay.Core.Network;

namespace GammaRay.Core.InternetAccess.Channels;

public interface IChannelDriver
{
	public ValueTask<ChannelOpeningResult> TryOpenChannelAsync(IAPChannel channel, WebEndPoint targetEndPoint);
}

public readonly struct ChannelOpeningResult : IAsyncDisposable
{
	private readonly object? _state;


	private ChannelOpeningResult(ResultType type, object? state)
	{
		Type = type;
		_state = state;
	}


	public ResultType Type { get; }

	public IOpenChannel OpenChannel => _state as IOpenChannel ?? throw new InvalidOperationException($"Result is {Type} do not provide open channel");

	public Exception? InternalException => _state as Exception;


	public static ChannelOpeningResult Success(IOpenChannel openChannel) => new(ResultType.Success, openChannel);

	public static ChannelOpeningResult ConnectionError(Exception? exception = null) => new(ResultType.ConnectionError, exception);

	public static ChannelOpeningResult Exception(Exception exception) => new(ResultType.Exception, exception);


	public ValueTask DisposeAsync()
	{
		if (Type == ResultType.Success)
			return ((IOpenChannel)_state!).DisposeAsync();
		return ValueTask.CompletedTask;
	}


	public enum ResultType
	{
		Success,
		ConnectionError,
		Exception
	}
}
