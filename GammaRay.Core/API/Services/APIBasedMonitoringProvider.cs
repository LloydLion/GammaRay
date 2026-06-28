using GammaRay.Core.Monitoring;
using GammaRay.Core.Monitoring.Converters;
using System.Text.Json;
using Google.Protobuf;
using System.Runtime.InteropServices;
using Google.Protobuf.WellKnownTypes;
using GammaRay.Core.API.Services.Proto;
using Guid = System.Guid;
using GGuid = GammaRay.Core.API.Services.Proto.Guid;

namespace GammaRay.Core.API.Services;

public sealed class APIBasedMonitoringProvider(
	MonitoringSerializerOptionsSource serializerOptionsSource
) : IMonitoringProvider
{
	private readonly HashSet<Listener> _listeners = [];
	private readonly JsonSerializerOptions _serializationOptions = serializerOptionsSource.JsonOptions;
	private IMonitoringSystemContext? _context;


	public void BindToSystem(MonitoringSystem monitoringSystem)
	{
		_context = monitoringSystem.Context;
	}

	public void NotifyNewProcedure(TrackableProcedure procedure)
	{
		SendEvent(new MonitoringEvent()
		{
			NewProcedure = new NewProcedureEvent()
			{
				CreationTime = Timestamp.FromDateTime(procedure.CreationTime),
				ProcedureId = CreateGGuid(procedure.Id),
				Type = procedure.Type
			}
		});
	}

	public void NotifyNewCommit(TrackableProcedure procedure, SystemReport newReport)
	{
		SendEvent(new MonitoringEvent() { CommitReport = CreateCommitReportMonitoringEvent(newReport) });
	}

	public void NotifyProcedureFinished(TrackableProcedure procedure)
	{
		SendEvent(new MonitoringEvent()
		{
			FinishProcedure = new FinishProcedureEvent()
			{
				ProcedureId = CreateGGuid(procedure.Id),
				ExceptionMessage = procedure.FatalException is not null ? procedure.FatalException.ToString() : "",
				IsSuccessful = procedure.IsSuccessful
			}
		});
	}

	public IDisposable Subscribe(Listener listener)
	{
		_listeners.Add(listener);
		return new Subscription(this, listener);
	}

	public IEnumerable<MonitoringEvent> GetPendingEvents()
	{
		if (_context is null)
			throw new InvalidOperationException("Bind provider first");

		var runningProcedures = _context.Procedures.Values;
		foreach (var procedure in runningProcedures)
		{
			yield return new MonitoringEvent
			{
				NewProcedure = new NewProcedureEvent
				{
					ProcedureId = CreateGGuid(procedure.Id),
					Type = procedure.Type,
					CreationTime = Timestamp.FromDateTime(procedure.CreationTime),
				}
			};

			foreach (var report in procedure.Reports)
			{
				yield return new MonitoringEvent
				{
					CommitReport = CreateCommitReportMonitoringEvent(report)
				};
			}
		}
	}

	private CommitReportEvent CreateCommitReportMonitoringEvent(SystemReport report)
	{
		var evt = new CommitReportEvent
		{
			ProcedureId = CreateGGuid(report.Procedure.Id),
			ClassIdentification = report.ClassIdentification
		};

		report.ReadProperties(new SerializingPropertyReader(_serializationOptions, evt));
		return evt;
	}

	private void SendEvent(MonitoringEvent monitoringEvent)
	{
		foreach (var listener in _listeners)
			listener(monitoringEvent);
	}

	private static GGuid CreateGGuid(Guid guid)
	{
		var gGuid = new GGuid();
		var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref guid, 1));
		gGuid.Ac = BitConverter.ToUInt64(span[..8]);
		gGuid.Dk = BitConverter.ToUInt64(span[8..]);
		return gGuid;
	}

	public delegate void Listener(MonitoringEvent monitoringEvent);

	public readonly struct Subscription(APIBasedMonitoringProvider _monitoringSystem, Listener _listener) : IDisposable
	{
		public void Dispose() => _monitoringSystem._listeners.Remove(_listener);
	}

	private readonly ref struct SerializingPropertyReader(JsonSerializerOptions _options, CommitReportEvent _destination) : ISystemReportReader
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
