namespace GammaRay.Core.Monitoring;

public sealed class MultiHeadMonitoringSystem : IMonitoringSystem
{
	private readonly IMonitoringSystem[] _nextSystems;


	public MultiHeadMonitoringSystem(IEnumerable<IMonitoringSystem> nextSystems)
	{
		_nextSystems = nextSystems.ToArray();
	}


	public void NewContext(MonitoringContext context)
	{
		foreach (var item in _nextSystems)
			try
			{ item.NewContext(context); }
			catch (Exception ex) { Console.WriteLine(ex); }
	}

	public void CloseContext(MonitoringContext context)
	{
		foreach (var item in _nextSystems)
			try { item.CloseContext(context); }
			catch (Exception ex) { Console.WriteLine(ex); }
	}

	public void NewReport(SystemReport report)
	{
		foreach (var item in _nextSystems)
			try { item.NewReport(report); }
			catch (Exception ex) { Console.WriteLine(ex); }
	}

	public void SetReportProperty<TProperty>(SystemReport report, string propertyName, ReportProperty<TProperty> oldValue, TProperty newValue)
	{
		foreach (var item in _nextSystems)
			try { item.SetReportProperty(report, propertyName, oldValue, newValue); }
			catch (Exception ex) { Console.WriteLine(ex); }
	}

	public void FinishReport(SystemReport report)
	{
		foreach (var item in _nextSystems)
			try { item.FinishReport(report); }
			catch (Exception ex) { Console.WriteLine(ex); }
	}
}
