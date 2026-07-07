using Avalonia.Controls;
using GammaRay.Client.GUI.ViewModels;

namespace GammaRay.Client.GUI;

public partial class ConnectServerWindow : Window
{
	public ConnectServerWindow()
	{
		InitializeComponent();
	}


	private void OnConnectButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		var viewModel = (ConnectServerWindowViewModel)DataContext!;

		Close(new ConnectServerWindowDialogResult(viewModel.HostName, viewModel.Port));
	}
}
