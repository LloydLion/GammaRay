using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.Net;

namespace GammaRay.Client.GUI.ViewModels;

public class ConnectionsViewModel : ViewModelBase
{
	public ConnectionsViewModel()
	{
		if (Design.IsDesignMode)
		{
			Connections = [
				new OnlineConnectionViewModel(
					"in-http",
					new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2313),
					Guid.Empty
				) { RoutingResult = "finlandProxy/vless", Status = "Established", Destination = "youtube.com:443" },

				new OnlineConnectionViewModel(
					"in-http",
					new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4121),
					Guid.Empty
				) { RoutingResult = "local-default/local", Status = "Established", Destination = "yandex.ru:443" },

				new OnlineConnectionViewModel(
					"in-http",
					new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5331),
					Guid.Empty
				) { RoutingResult = "finlandProxy/shadowsocks", Status = "Established", Destination = "instagram.com:443" }
			];
		}
	}


	public ObservableCollection<OnlineConnectionViewModel> Connections { get; } = [];
}
