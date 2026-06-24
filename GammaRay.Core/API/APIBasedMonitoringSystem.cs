using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using System.Text.Json;
using System.Threading.Channels;
using GammaRay.Core.API.Proto;
using Google.Protobuf;
using System.Runtime.InteropServices;

namespace GammaRay.Core.API;

public sealed class APIBasedMonitoringSystem(
	MonitoringSerializerOptionsSource serializerOptionsSource,
	MonitoringEventBuffer _eventBuffer
) : IMonitoringSystem
{
	private readonly HashSet<Listener> _listeners = [];
	private readonly JsonSerializerOptions _serializationOptions = serializerOptionsSource.JsonOptions;
	

	public void NewContext(MonitoringContext context)
	{
		_eventBuffer.Save(context);
		SendEvent(new MonitoringEvent
		{
			NewContext = new NewContextEvent
			{
				CreationTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(context.CreationTime),
				ContextId = CreateByteString(context.Id),
				Type = context.Type
			}
		});
	}

	public void CloseContext(MonitoringContext context)
	{
		_eventBuffer.Discard(context);
		SendEvent(new MonitoringEvent
		{
			CloseContext = new CloseContextEvent
			{
				ContextId = CreateByteString(context.Id)
			}
		});
	}

	public void NewReport(SystemReport report)
	{
		_eventBuffer.Save(report);
		SendEvent(new MonitoringEvent
		{
			NewReport = new NewReportEvent
			{
				ContextId = CreateByteString(report.MonitoringContext.Id),
				ReportType = report.GetType().FullName
			}
		});
	}

	public void FinishReport(SystemReport report)
	{
		SendEvent(CreateFinishReportEvent(report));
	}

	public void SetReportProperty<TProperty>(SystemReport report, string propertyName, ReportProperty<TProperty> oldValue, TProperty newValue) { }

	public IDisposable Subscribe(Listener listener)
	{
		_listeners.Add(listener);
		return new Subscription(this, listener);
	}

	public IEnumerable<MonitoringEvent> GetPendingEvents()
	{
		var pendingContexts = _eventBuffer.RestoreAll();
		foreach (var pendingContextState in pendingContexts)
		{
			var context = pendingContextState.Context;
			yield return new MonitoringEvent
			{
				NewContext = new NewContextEvent
				{
					CreationTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(context.CreationTime),
					ContextId = CreateByteString(context.Id),
					Type = context.Type
				}
			};

			foreach (var report in pendingContextState.OpenReports)
			{
				yield return new MonitoringEvent
				{
					NewReport = new NewReportEvent
					{
						ContextId = CreateByteString(report.MonitoringContext.Id),
						ReportType = report.GetType().FullName
					}
				};

				if (report.Finished)
				{
					yield return CreateFinishReportEvent(report);
				}
			}
		}
	}

	private void SendEvent(MonitoringEvent monitoringEvent)
	{
		foreach (var listener in _listeners)
			listener(monitoringEvent);
	}

	private static ByteString CreateByteString(Guid id)
	{
		return ByteString.CopyFrom(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref id, 1)));
	}

	private MonitoringEvent CreateFinishReportEvent(SystemReport report)
	{
		var finishEvent = new FinishReportEvent
		{
			ContextId = CreateByteString(report.MonitoringContext.Id),
			Component = report.Component
		};

		report.ReadProperties(new SerializingPropertyReader(_serializationOptions, finishEvent));

		return new MonitoringEvent
		{
			FinishReport = finishEvent
		};
	}


	public delegate void Listener(MonitoringEvent monitoringEvent);

	public readonly struct Subscription(APIBasedMonitoringSystem _monitoringSystem, Listener _listener) : IDisposable
	{
		public void Dispose() => _monitoringSystem._listeners.Remove(_listener);
	}

	private readonly ref struct SerializingPropertyReader(JsonSerializerOptions _options, FinishReportEvent _destination) : ISystemReportReader
	{
		public void FeedProperty<TProperty>(string propertyName, ReportProperty<TProperty> property)
		{
			if (property.IsSet)
			{
				var jsonValue = JsonSerializer.Serialize(property.Value, _options);
				_destination.JsonReport.Add(propertyName, jsonValue);
			}
		}
	}
}
