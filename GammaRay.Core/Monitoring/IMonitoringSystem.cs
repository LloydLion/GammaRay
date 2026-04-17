namespace GammaRay.Core.Monitoring;

public interface IMonitoringSystem
{
	public void NewContext(MonitoringContext context);

	public void CloseContext(MonitoringContext context);

	public void NewReport(SystemReport report);

	public void SetReportProperty<TProperty>(
		SystemReport report,
		string propertyName,
		ReportProperty<TProperty> oldValue,
		TProperty newValue
	);

	public void FinishReport(SystemReport report);
}
