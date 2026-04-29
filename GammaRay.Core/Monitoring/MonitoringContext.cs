using System.Reflection;

namespace GammaRay.Core.Monitoring;

public sealed class MonitoringContext : IDisposable
{
	private static readonly Dictionary<Type, MethodInfo> _newReportMethodImplementationCache = [];


	public MonitoringContext(string type, TimeProvider time, IMonitoringSystem monitoringSystem)
		: this(type, time.GetUtcNow().UtcDateTime, monitoringSystem) { }

	public MonitoringContext(string type, DateTime creationTime, IMonitoringSystem monitoringSystem)
		: this(type, creationTime, monitoringSystem, Guid.NewGuid()) { }

	public MonitoringContext(string type, DateTime creationTime, IMonitoringSystem monitoringSystem, Guid predefinedId)
	{
		Type = type;
		MonitoringSystem = monitoringSystem;
		Id = predefinedId;
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

	public SystemReport NewReport(Type reportType)
	{
		if (_newReportMethodImplementationCache.TryGetValue(reportType, out var method) == false)
		{
			method = GetType()
				.GetMethod(nameof(NewReport), genericParameterCount: 1, BindingFlags.Instance | BindingFlags.Public, [])!
				.MakeGenericMethod([reportType]);
			_newReportMethodImplementationCache.Add(reportType, method);
		}

		return (SystemReport)method.Invoke(this, null)!;
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
