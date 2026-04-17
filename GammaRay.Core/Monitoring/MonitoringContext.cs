namespace GammaRay.Core.Monitoring;

public sealed class MonitoringContext : IDisposable
{
	public MonitoringContext(string type, TimeProvider time, IMonitoringSystem monitoringSystem)
		: this(type, time.GetUtcNow().UtcDateTime, monitoringSystem) { }

	public MonitoringContext(string type, DateTime creationTime, IMonitoringSystem monitoringSystem)
	{
		Type = type;
		MonitoringSystem = monitoringSystem;
		Id = Guid.NewGuid();
		CreationTime = creationTime;

		monitoringSystem.NewContext(this);
	}


	public string Type { get; }

	public IMonitoringSystem MonitoringSystem { get; }

	public Guid Id { get; }

	public DateTime CreationTime { get; }


	public TReport NewReport<TReport>() where TReport : SystemReport, new()
	{
		var report = new TReport();
		report.SetContext(this);
		MonitoringSystem.NewReport(report);
		return report;
	}

	public void Close()
	{
		MonitoringSystem.CloseContext(this);
	}

	void IDisposable.Dispose() => Close();

	internal void NotifyReportChanged<TProperty>(SystemReport report, string propertyName, ReportProperty<TProperty> oldValue, TProperty newValue)
	{
		MonitoringSystem.SetReportProperty(report, propertyName, oldValue, newValue);
	}

	internal void NotifyReportFinished(SystemReport report)
	{
		MonitoringSystem.FinishReport(report);
	}
}
