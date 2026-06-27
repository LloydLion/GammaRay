namespace GammaRay.Core.Monitoring;

public interface IMonitoringProvider
{
	public void BindToSystem(MonitoringSystem monitoringSystem) { }

	public void NotifyNewProcedure(TrackableProcedure procedure);

	public void NotifyNewCommit(TrackableProcedure procedure, SystemReport newReport);

	public void NotifyProcedureFinished(TrackableProcedure procedure);
}
