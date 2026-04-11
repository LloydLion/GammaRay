using System.Data;
using System.Threading.Channels;

namespace GammaRay.Core.Persistence;

public abstract class AsyncDbRepository<TState, TStateMutation> : IAsyncDisposable
	where TState : class
	where TStateMutation : notnull
{
	private Task? _writerTask;
	private TState? _state;
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly Channel<TStateMutation> _writeChannel;


	public AsyncDbRepository(IDbConnectionFactory connectionFactory)
	{
		_writeChannel = Channel.CreateUnbounded<TStateMutation>(
			new UnboundedChannelOptions()
			{
				SingleReader = true,
				SingleWriter = false
			}
		);
		_connectionFactory = connectionFactory;
	}


	protected TState CurrentState => _state ??
		throw new InvalidOperationException("Storage not initialized. Call Initialize() before use");


	public void Initialize()
	{
		_writerTask = Task.Run(WriteLoop);
		using var connection = _connectionFactory.CreateNewConnection();
		PerformDatabaseMigration(connection);
		_state = PreloadData(connection);
	}

	public async ValueTask DisposeAsync()
	{
		_writeChannel.Writer.Complete();
		if (_writerTask is not null)
			await _writerTask;
	}


	protected void Write(TStateMutation mutation)
	{
		ApplyMutation(CurrentState, mutation);
		_writeChannel.Writer.TryWrite(mutation);
	}

	protected abstract ValueTask ExecuteWriteAsync(IDbConnection connection, TStateMutation item);

	protected abstract TState PreloadData(IDbConnection connection);

	protected abstract void ApplyMutation(TState state, TStateMutation mutation);

	protected abstract void PerformDatabaseMigration(IDbConnection connection);

	private async Task WriteLoop()
	{
		while (await _writeChannel.Reader.WaitToReadAsync())
		{
			using var connection = _connectionFactory.CreateNewConnection();
			while (_writeChannel.Reader.TryRead(out var item))
			{
				try
				{
					await ExecuteWriteAsync(connection, item);
				}
				catch (Exception)
				{

				}
			}
		}
	}
}
