using System.Diagnostics;

namespace GammaRay.Core.Monitoring;

public class MonitoringSystem
{
	private readonly MonitoringContext _context = new();
	private readonly IMonitoringProvider[] _providers;


	public MonitoringSystem(IEnumerable<IMonitoringProvider> providers)
	{
		_providers = providers.ToArray();
		foreach (var provider in _providers)
			provider.BindToSystem(this);
	}


	public IMonitoringSystemContext Context => _context;


	public void NotifyNewProcedure(TrackableProcedure procedure)
	{
		_context.Procedures.Add(procedure.Id, procedure);
		ExecuteProviderMethod(procedure, (p, a) => p.NotifyNewProcedure(a));
	}

	public void NotifyNewCommit(TrackableProcedure procedure, SystemReport newReport)
	{
		ExecuteProviderMethod((procedure, newReport), (p, a) => p.NotifyNewCommit(a.procedure, a.newReport));
	}

	public void NotifyProcedureFinished(TrackableProcedure procedure)
	{
		_context.Procedures.Remove(procedure.Id);
		ExecuteProviderMethod(procedure, (p, a) => p.NotifyProcedureFinished(a));
	}

	private void ExecuteProviderMethod<TContext>(TContext ctx, Action<IMonitoringProvider, TContext> method)
	{
		foreach (var provider in _providers)
		{
			try
			{
				method(provider, ctx);
			}
			catch (Exception ex) { Debugger.BreakForUserUnhandledException(ex); }
		}
	}

	private class MonitoringContext : IMonitoringSystemContext
	{
		public Dictionary<Guid, TrackableProcedure> Procedures { get; } = new();

		IReadOnlyDictionary<Guid, TrackableProcedure> IMonitoringSystemContext.Procedures => Procedures;
	}
}
