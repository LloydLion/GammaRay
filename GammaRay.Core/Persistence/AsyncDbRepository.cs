using Serilog;
using System.Data;
using System.Threading.Channels;

namespace GammaRay.Core.Persistence;

public abstract class AsyncDbRepository<TKey, TValue> : IAsyncDisposable
	where TKey : notnull
	where TValue : notnull
{
	private Task? _writerTask;
	private Dictionary<TKey, TValue>? _data;
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly ILogger _logger;
	private readonly Channel<TValue> _writeChannel;

	public AsyncDbRepository(IDbConnectionFactory connectionFactory, ILogger logger)
	{
		_writeChannel = Channel.CreateUnbounded<TValue>(
			new UnboundedChannelOptions()
			{
				SingleReader = true,
				SingleWriter = false
			}
		);
		_connectionFactory = connectionFactory;
		_logger = logger;
	}


	private Dictionary<TKey, TValue> Data => _data ??
		throw new InvalidOperationException("Storage not initialized. Call Initialize() before use");


	public void Initialize()
	{
		_writerTask = Task.Run(WriteLoop);
		using var connection = _connectionFactory.CreateNewConnection();
		PerformDatabaseMigration(connection);
		_data = PreloadData(connection).ToDictionary(ExtractKey);
	}

	public async ValueTask DisposeAsync()
	{
		_writeChannel.Writer.Complete();
		if (_writerTask is not null)
			await _writerTask;
	}

	protected void Write(TValue value)
	{
		Data[ExtractKey(value)] = value;
		_writeChannel.Writer.TryWrite(value);
	}

	protected TValue? TryRead(TKey key)
	{
		Data.TryGetValue(key, out var value);
		return value;
	}

	protected abstract ValueTask ExecuteWriteAsync(IDbConnection connection, TValue item);

	protected abstract IEnumerable<TValue> PreloadData(IDbConnection connection);

	protected abstract TKey ExtractKey(TValue value);

	protected abstract void PerformDatabaseMigration(IDbConnection connection);

	private async Task WriteLoop()
	{
		try
		{
			while (await _writeChannel.Reader.WaitToReadAsync())
			{
				using var connection = _connectionFactory.CreateNewConnection();
				while (_writeChannel.Reader.TryRead(out var item))
				{
					await ExecuteWriteAsync(connection, item);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "WriterLoop failed");
		}
	}
}
