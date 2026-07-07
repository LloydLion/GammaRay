using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GammaRay.Client.GUI.ViewModels;
using GammaRay.Client.GUI.Views;
using GammaRay.Core.API.Client;
using GammaRay.Core.Inbound;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using GammaRay.Core.Routing;
using GammaRay.Core.Utils.FileSystem;

namespace GammaRay.Client.GUI;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);

#if DEBUG
		this.AttachDeveloperTools();
#endif
	}

	public override async void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var mainWindow = new MainWindow();
			var viewModel = new MainViewModel(new GammaRayAPIClient(), mainWindow);
			mainWindow.DataContext = viewModel;

			desktop.MainWindow = mainWindow;
		}

		base.OnFrameworkInitializationCompleted();
	}
}

public class DummyLocator : IFileSystemLocator
{
	public bool Exists(string filePath) => true;

	public void Move(string originalFilePath, string newFilePath, bool overwrite = false) { }

	public Stream Open(string path, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, FileShare share = FileShare.None)
	{
		return Stream.Null;
	}
}

public class MonitoringConnectionTracker(ICollection<OnlineConnectionViewModel> output) : IMonitoringProvider
{
	public void NotifyNewProcedure(TrackableProcedure procedure)
	{

	}

	public void NotifyNewCommit(TrackableProcedure procedure, SystemReport newReport)
	{
		if (newReport.Procedure.Type != "Connection")
			return;

		switch (newReport)
		{
			case HTTPInboundDriver.Report httpReport:
				{
					if (httpReport.RemoteEndPoint.IsSet == false || httpReport.DestinationEndPoint.IsSet == false)
						return;

					var newConnection = new OnlineConnectionViewModel("unk", "HTTP", httpReport.RemoteEndPoint.Value, new WebEndPoint(httpReport.DestinationEndPoint.Value, TransportType.StreamBased), procedure.Id);
					output.Add(newConnection);
					break;
				}

			case SOCKS5InboundDriver.Report socksReport:
				{
					if (socksReport.RemoteEndPoint.IsSet == false || socksReport.DestinationEndPoint.IsSet == false)
						return;

					var newConnection = new OnlineConnectionViewModel("unk", "SOCKS5", socksReport.RemoteEndPoint.Value, socksReport.DestinationEndPoint.Value, procedure.Id);
					output.Add(newConnection);
					break;
				}

			case SmartRouter.Report smartRouterReport:
				{
					if (smartRouterReport.ResultIAP.IsSet == false || smartRouterReport.ResultChannelName.IsSet == false)
						return;

					var connection = output.FirstOrDefault(c => c.Id == smartRouterReport.Procedure.Id);
					if (connection is null)
						return;

					connection.RoutingResult = $"{smartRouterReport.ResultIAP.Value}/{smartRouterReport.ResultChannelName.Value}";
					break;
				}
		}
	}

	public void NotifyProcedureFinished(TrackableProcedure procedure)
	{
		var connection = output.FirstOrDefault(c => c.Id == procedure.Id);
		if (connection is not null)
			output.Remove(connection);
	}
}
