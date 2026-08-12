using Nito.AsyncEx;
using System.Diagnostics;

namespace GammaRay.Core.Host;

public delegate Task ApplicationRunDelegate(ApplicationControl applicationControl, CancellationToken globalStopToken);

public class ApplicationControl(ApplicationRunDelegate applicationRunDelegate)
{
	private CancellationTokenSource? _cts;
	private bool _shouldExit = false;
	private readonly ApplicationRunDelegate _applicationRunDelegate = applicationRunDelegate;


	public ApplicationStatus Status { get; private set; }

	public CancellationToken GlobalStopToken => _cts?.Token ?? throw new InvalidOperationException("Application in invalid state");


	public void MainLoop()
	{
		AsyncContext.Run(async () =>
		{
			while (_shouldExit == false)
			{
				GC.Collect();
				GC.WaitForFullGCComplete();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				GC.WaitForFullGCComplete();

				Status = ApplicationStatus.Running;
				_cts = new CancellationTokenSource();
				try
				{
					await _applicationRunDelegate(this, _cts.Token);
				}
				catch (OperationCanceledException) { }
			}
			Status = ApplicationStatus.Halted;
		});
	}

	public void Shutdown()
	{
		_shouldExit = true;
		Status = ApplicationStatus.ShuttingDown;
		_cts!.Cancel();
	}

	public void Restart()
	{
		_shouldExit = false;
		Status = ApplicationStatus.Rebooting;
		_cts!.Cancel();
	}
}
