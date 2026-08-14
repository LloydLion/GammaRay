using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GammaRay.Client.GUI.ViewModels;
using GammaRay.Client.GUI.Views;
using GammaRay.Core.API.Client;
using GammaRay.Core.Connection;
using GammaRay.Core.Monitoring;

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
			case MasterServer.NewConnectionReport newConnectionReport:
				{
					if (newConnectionReport.RemoteEndPoint.IsSet == false || newConnectionReport.Inbound.IsSet == false)
						return;

					var newConnection = new OnlineConnectionViewModel(newConnectionReport.Inbound.Value, newConnectionReport.RemoteEndPoint.Value, procedure.Id);
					output.Add(newConnection);
					break;
				}

			case MasterServer.ConnectionRequestReport connectionRequestedReport:
				{
					if (connectionRequestedReport.DestinationEndPoint.IsSet == false)
						return;

					var connection = output.FirstOrDefault(c => c.Id == connectionRequestedReport.Procedure.Id);
					if (connection is null)
						return;

					var destination = connectionRequestedReport.DestinationEndPoint.Value;
					connection.Destination = $"{destination.Host}:{destination.Port}";
					connection.Status = "Routed";
					break;
				}

			case MasterServer.ConnectionRoutedReport routedReport:
				{
					if (routedReport.RoutingResult.IsSet == false)
						return;
					var connection = output.FirstOrDefault(c => c.Id == routedReport.Procedure.Id);
					if (connection is null)
						return;
					var routingResult = routedReport.RoutingResult.Value;
					connection.RoutingResult = $"{routingResult.IAP.Name}/{routingResult.ChannelName}";
					break;
				}

			case MasterServer.ConnectionEstablishedReport establishedReport:
				{
					var connection = output.FirstOrDefault(c => c.Id == establishedReport.Procedure.Id);
					if (connection is null)
						return;

					connection.Status = "Established";
					break;
				}

			case MasterServer.ConnectionStaleReport staleReport:
				{
					if (staleReport.IsStale.IsSet == false)
						return;
					var connection = output.FirstOrDefault(c => c.Id == staleReport.Procedure.Id);
					if (connection is null)
						return;
					connection.Status = staleReport.IsStale.Value ? "Stale" : "Established";
					break;
				}

			case MasterServer.ConnectionReroutedReport reroutedReport:
				{
					var connection = output.FirstOrDefault(c => c.Id == reroutedReport.Procedure.Id);
					if (connection is null)
						return;
					var routingResult = reroutedReport.ReroutingResult.Value;
					connection.RoutingResult = $"[R] {routingResult.IAP.Name}/{routingResult.ChannelName}";
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
