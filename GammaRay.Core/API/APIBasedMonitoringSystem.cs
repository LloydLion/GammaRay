using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using GammaRay.Core.Utils;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace GammaRay.Core.API;

public sealed class APIBasedMonitoringSystem(
	MonitoringSerializerOptionsSource serializerOptionsSource,
	MonitoringEventBuffer _eventBuffer
) : IMonitoringSystem, IAsyncDisposable
{
	private readonly Dictionary<IAPIEventSink, TranslationConfiguration> _listeners = [];
	private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create();
	private readonly Channel<TranslationTask> _translationChannel = Channel.CreateUnbounded<TranslationTask>(
		new() { SingleReader = true, SingleWriter = false }
	);
	private readonly JsonSerializerOptions _serializationOptions = serializerOptionsSource.JsonOptions;
	private Task? _translationTask;


	public void Initialize()
	{
		_translationTask = Task.Run(MainTranslationLoop);
	}

	public async ValueTask DisposeAsync()
	{
		_translationChannel.Writer.Complete();
		if (_translationTask is not null)
			await _translationTask;
	}

	public void NewContext(MonitoringContext context)
	{
		_eventBuffer.Save(context);

		if (IsAnyoneListening(MessageType.StandardEvent) == false)
			return;
		var buffer = CreateBuffer(APIConstants.EventType.MonitoringNewContext);
		var writer = new BufferWriter(buffer, initialUsedLength: 2);
		writer.WriteDateTime(context.CreationTime);
		writer.WriteGuid(context.Id);
		writer.WriteString(context.Type, Encoding.UTF8);
		_translationChannel.Writer.TryWrite(new(buffer, writer.UsedLength));
	}

	public void CloseContext(MonitoringContext context)
	{
		_eventBuffer.Discard(context);

		if (IsAnyoneListening(MessageType.StandardEvent) == false)
			return;
		var buffer = CreateBuffer(APIConstants.EventType.MonitoringCloseContext);
		var writer = new BufferWriter(buffer, initialUsedLength: 2);
		writer.WriteGuid(context.Id);
		_translationChannel.Writer.TryWrite(new(buffer, writer.UsedLength));
	}

	public void NewReport(SystemReport report)
	{
		_eventBuffer.Save(report);

		if (IsAnyoneListening(MessageType.StandardEvent) == false)
			return;
		var buffer = CreateBuffer(APIConstants.EventType.MonitoringNewReport);
		var writer = new BufferWriter(buffer, initialUsedLength: 2);
		writer.WriteGuid(report.MonitoringContext.Id);
		writer.WriteString(report.GetType().FullName, Encoding.UTF8);
		_translationChannel.Writer.TryWrite(new(buffer, writer.UsedLength));
	}

	public void FinishReport(SystemReport report)
	{
		//_eventBuffer.Discard(report);

		if (IsAnyoneListening(MessageType.StandardEvent) == false)
			return;
		var buffer = CreateBuffer(APIConstants.EventType.MonitoringFinishReport);
		var writer = new BufferWriter(buffer, initialUsedLength: 2);
		writer.WriteGuid(report.MonitoringContext.Id);
		writer.WriteStringWithLength(report.Component, Encoding.UTF8);

		var wrote = 0;
		report.ReadProperties(new PropertyReader(writer.UnusedBufferPart, ref wrote, this));
		writer.Advance(wrote);

		_translationChannel.Writer.TryWrite(new(buffer, writer.UsedLength));
	}

	public void SetReportProperty<TProperty>(SystemReport report, string propertyName, ReportProperty<TProperty> oldValue, TProperty newValue)
	{
		if (IsAnyoneListening(MessageType.PropertyUpdateEvent) == false)
			return;
		var buffer = CreateBuffer(APIConstants.EventType.MonitoringSetReportProperty);
		var writer = new BufferWriter(buffer, initialUsedLength: 2);

		writer.WriteGuid(report.MonitoringContext.Id);
		writer.WriteStringWithLength(report.Component, Encoding.UTF8);
		writer.WriteStringWithLength(propertyName, Encoding.UTF8);

		try
		{
			var serializedValue = JsonSerializer.Serialize(newValue, _serializationOptions);
			writer.WriteString(serializedValue, Encoding.UTF8);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Serialization error: {report.Component}.{propertyName} {newValue}");
			Console.WriteLine(ex);
			_pool.Return(buffer);
			return;
		}

		_translationChannel.Writer.TryWrite(new(buffer, writer.UsedLength));
	}


	public void ConfigureTranslation(IAPIEventSink eventSink, TranslationConfiguration configuration) => _listeners[eventSink] = configuration;

	public void StopTranslation(IAPIEventSink eventSink) => _listeners.Remove(eventSink);

	public int WritePendingMonitoringEvents(Span<byte> buffer)
	{
		var pendingContexts = _eventBuffer.RestoreAll();
		var writer = new BufferWriter(buffer);
		foreach (var pendingContextState in pendingContexts)
		{
			var context = pendingContextState.Context;
			writer.WriteDateTime(context.CreationTime);
			writer.WriteGuid(context.Id);
			writer.WriteStringWithLength(context.Type, Encoding.UTF8);
			writer.WriteInt(pendingContextState.OpenReports.Count);
			foreach (var report in pendingContextState.OpenReports)
			{
				writer.WriteStringWithLength(report.GetType().FullName, Encoding.UTF8);
				writer.WriteBoolean(report.Finished);
				var wrote = 0;
				report.ReadProperties(new PropertyReader(writer.UnusedBufferPart, ref wrote, this));
				writer.Advance(wrote);
				writer.WriteInt(-1);
			}
		}

		return writer.UsedLength;
	}

	private bool IsAnyoneListening(MessageType type) => _listeners.Values.Any(configuration => IsListening(configuration, type));

	private byte[] CreateBuffer(APIConstants.EventType eventType)
	{
		var buffer = _pool.Rent(APIConstants.AllocationBufferSize);
		buffer[1] = (byte)eventType;
		return buffer;
	}

	private async Task MainTranslationLoop()
	{
		while (await _translationChannel.Reader.WaitToReadAsync())
		{
			while (_translationChannel.Reader.TryRead(out var task))
			{
				var data = task.Data.AsMemory(..task.Length);
				await TranslateEventAsync(data, task.Type);
				_pool.Return(task.Data);
			}
		}
	}

	private async ValueTask TranslateEventAsync(Memory<byte> data, MessageType type)
	{
		foreach (var (eventSink, configuration) in _listeners)
		{
			if (IsListening(configuration, type) == false)
				continue;
			await eventSink.SendEvent(data);
		}
	}

	private static bool IsListening(in TranslationConfiguration configuration, MessageType type)
	{
		if (configuration.Enabled == false)
			return false;
		if (configuration.PropertyTranslationEnabled == false && type == MessageType.PropertyUpdateEvent)
			return false;
		return true;
	}


	public struct TranslationConfiguration
	{
		public bool Enabled { get; set; }

		public bool PropertyTranslationEnabled { get; set; }
	}

	private record struct TranslationTask(byte[] Data, int Length, MessageType Type = MessageType.StandardEvent);

	private enum MessageType
	{
		StandardEvent = 1,
		PropertyUpdateEvent = 2
	}

	private ref struct PropertyReader(Span<byte> output, ref int _wrote, APIBasedMonitoringSystem _owner) : ISystemReportReader
	{
		private readonly Span<byte> _output = output;
		private ref int _wrote = ref _wrote;


		public void FeedProperty<TProperty>(string propertyName, ReportProperty<TProperty> property)
		{
			var writer = new BufferWriter(_output, initialUsedLength: _wrote);
			try
			{
				writer.WriteStringWithLength(propertyName, Encoding.UTF8);

				if (property.IsSet == false)
				{
					writer.WriteInt(0);
					return;
				}

				try
				{
					var serializedProperty = JsonSerializer.Serialize(property.Value, _owner._serializationOptions);
					writer.WriteStringWithLength(serializedProperty, Encoding.UTF8);
				}
				catch (Exception ex)
				{
					Debugger.BreakForUserUnhandledException(ex);
					writer.WriteInt(0);
					return;
				}
			}
			finally
			{
				_wrote = writer.UsedLength;
			}
		}
	}
}
