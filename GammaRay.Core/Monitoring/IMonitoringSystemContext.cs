namespace GammaRay.Core.Monitoring;

public interface IMonitoringSystemContext
{
	public IReadOnlyDictionary<Guid, TrackableProcedure> Procedures { get; }
}
