using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Monitoring;

public sealed class TrackableProcedure : IDisposable
{
	private readonly List<SystemReport> _reports = [];


	private TrackableProcedure(string type, DateTime creationTime, MonitoringSystem monitoringSystem, Guid predefinedId)
	{
		Type = type;
		MonitoringSystem = monitoringSystem;
		Id = predefinedId;
		CreationTime = creationTime;
	}

	public static TrackableProcedure New(string type, TimeProvider creationTime, MonitoringSystem monitoringSystem) =>
		New(type, creationTime.GetUtcNow().UtcDateTime, monitoringSystem);

	public static TrackableProcedure New(string type, DateTime creationTime, MonitoringSystem monitoringSystem) =>
		New(type, creationTime, monitoringSystem, Guid.NewGuid());

	public static TrackableProcedure New(string type, DateTime creationTime, MonitoringSystem monitoringSystem, Guid predefinedId)
	{
		var procedure = new TrackableProcedure(type, creationTime, monitoringSystem, predefinedId);
		monitoringSystem.NotifyNewProcedure(procedure);
		return procedure;
	}


	public string Type { get; }

	public MonitoringSystem MonitoringSystem { get; }

	public Guid Id { get; }

	public DateTime CreationTime { get; }

	public TrackableProcedureStatus Status { get; private set; } = TrackableProcedureStatus.Running;

	public Exception? FatalException { get; private set; }

	public IReadOnlyList<SystemReport> Reports => _reports;


	public bool IsRunning => Status == TrackableProcedureStatus.Running;

	public bool IsCompleted => IsRunning == false;

	[MemberNotNullWhen(true, nameof(FatalException))]
	public bool IsFailed => IsCompleted && FatalException is not null;

	[MemberNotNullWhen(false, nameof(FatalException))]
	public bool IsSuccessful => IsCompleted && FatalException is null;


	public void CommitReport(SystemReport report)
	{
		var bindArgs = new SystemReportBindingParameters(_reports.Count);
		report.BindProcedure(this, bindArgs);
		_reports.Add(report);

		MonitoringSystem.NotifyNewCommit(this, report);
	}

	public void Finish()
	{
		if (IsCompleted)
			return;

		Status = TrackableProcedureStatus.Completed;
		MonitoringSystem.NotifyProcedureFinished(this);
	}

	public void SetFatalException(Exception exception)
	{
		Debug.Assert(IsRunning, "Tried to set fatal exception on a procedure that is completed");
		Debug.Assert(FatalException is null, "Tried to set fatal exception on a procedure that already has one");
		FatalException = exception;
	}

	void IDisposable.Dispose() => Finish();
}
