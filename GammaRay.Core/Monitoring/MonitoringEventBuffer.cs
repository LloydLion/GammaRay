using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GammaRay.Core.Monitoring;

public sealed class MonitoringEventBuffer
{
	private SavedMonitoringContextState[]? _pool;
	private readonly Dictionary<Guid, SavedMonitoringContextState> _buffered = [];


	public void Save(MonitoringContext context)
	{
		if (_buffered.TryGetValue(context.Id, out var state))
		{
			state.RefCounter++;
			return;
		}

		_buffered.Add(context.Id, RequestNewSavedMonitoringContextState(context));
	}

	public void Discard(MonitoringContext context) => Discard(context.Id);

	public void Discard(Guid contextId)
	{
		if (_buffered.TryGetValue(contextId, out var state))
		{
			state.RefCounter--;
			if (state.RefCounter <= 0)
				_buffered.Remove(contextId);
			state.Reset();
		}
	}

	public void Save(SystemReport report)
	{
		if (_buffered.TryGetValue(report.MonitoringContext.Id, out var state))
		{
			ref var refCount = ref CollectionsMarshal.GetValueRefOrAddDefault(state.OpenReports, report, out _);
			refCount++;
		}
	}

	public void Discard(SystemReport report)
	{
		if (_buffered.TryGetValue(report.MonitoringContext.Id, out var state))
		{
			ref var refCount = ref CollectionsMarshal.GetValueRefOrNullRef(state.OpenReports, report);
			if (Unsafe.IsNullRef(ref refCount) == false)
			{
				refCount--;
				if (refCount == 0)
					state.OpenReports.Remove(report);
			}
		}
	}

	public ISavedMonitoringContextState? TryRestore(MonitoringContext context) => TryRestore(context.Id);

	public ISavedMonitoringContextState? TryRestore(Guid contextId)
	{
		if (_buffered.TryGetValue(contextId, out var state))
			return state;
		return null;
	}

	public IReadOnlyCollection<ISavedMonitoringContextState> RestoreAll() => _buffered.Values;

	private SavedMonitoringContextState RequestNewSavedMonitoringContextState(MonitoringContext context)
	{
		if (_pool is null)
		{
			_pool = new SavedMonitoringContextState[16];
			for (int i = 0; i < _pool.Length; i++)
				_pool[i] = new();
		}
		
		for (int i = 0; i < _pool.Length; i++)
		{
			var state = _pool[i];
			if (state.InUse == false)
			{
				state.Initialize(context);
				return state;
			}
		}

		var baseLen = _pool.Length;
		Array.Resize(ref _pool, baseLen * 2);
		for (int i = 0; i < baseLen; i++)
			_pool[i + baseLen] = new();
		var result = _pool[baseLen];
		result.Initialize(context);
		return result;
	}


	public interface ISavedMonitoringContextState
	{
		public MonitoringContext Context { get; }

		public IReadOnlyCollection<SystemReport> OpenReports { get; }
	}

	public class SavedMonitoringContextState : ISavedMonitoringContextState
	{
		private MonitoringContext? _context;


		public MonitoringContext Context => _context ?? throw new InvalidOperationException("Initialize first");

		public int RefCounter { get; set; } = 1;

		public Dictionary<SystemReport, int> OpenReports { get; } = [];

		IReadOnlyCollection<SystemReport> ISavedMonitoringContextState.OpenReports => OpenReports.Keys;

		public bool InUse => RefCounter > 0;


		public void Initialize(MonitoringContext context)
		{
			_context = context;
			RefCounter = 1;
		}

		public void Reset()
		{
			_context = null!;
			RefCounter = 0;
			OpenReports.Clear();
		}
	}
}
