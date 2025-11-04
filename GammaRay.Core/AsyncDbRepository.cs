using GammaRay.Core.Persistence;
using Serilog;
using System.Threading.Channels;

namespace GammaRay.Core;

public abstract class AsyncDbRepository<TKey, TValue> : IAsyncDisposable
	where TKey : notnull
	where TValue : notnull
{
	private Task? _writerTask;
	private Dictionary<TKey, TValue>? _data;
	private readonly AppDbContext _context;
	private readonly ILogger _logger;
	private readonly Channel<TValue> _writeChannel;

	public AsyncDbRepository(AppDbContext context, ILogger logger)
	{
		_writeChannel = Channel.CreateUnbounded<TValue>(
			new UnboundedChannelOptions()
			{
				SingleReader = true,
				SingleWriter = false
			}
		);
		_context = context;
		_logger = logger;
	}


	private Dictionary<TKey, TValue> Data => _data ??
		throw new InvalidOperationException("Storage not initialized. Call Initialize() before use");


	public void Initialize()
	{
		_writerTask = Task.Run(WriteLoop);
		_data = PreloadData(_context).ToDictionary(ExtractKey);
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

	protected abstract ValueTask ExecuteWriteAsync(AppDbContext context, TValue item);

	protected abstract IEnumerable<TValue> PreloadData(AppDbContext context);

	protected abstract TKey ExtractKey(TValue value);

	private async Task WriteLoop()
	{
		try
		{
			while (await _writeChannel.Reader.WaitToReadAsync())
			{
				while (_writeChannel.Reader.TryRead(out var item))
				{
					await ExecuteWriteAsync(_context, item);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "WriterLoop failed");
		}
	}
}
