using Avalonia.Controls;
using GammaRay.Core.Network;
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
					"HTTP",
					new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2313),
					new WebEndPoint(new WebHost("youtube.com"), 443, TransportType.StreamBased),
					Guid.Empty
				) { RoutingResult = "finlandProxy/vless" },

				new OnlineConnectionViewModel(
					"in-http",
					"HTTP",
					new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4121),
					new WebEndPoint(new WebHost("yandex.ru"), 443, TransportType.StreamBased),
					Guid.Empty
				) { RoutingResult = "local-default/local" },

				new OnlineConnectionViewModel(
					"in-http",
					"HTTP",
					new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5331),
					new WebEndPoint(new WebHost("instagram.com"), 443, TransportType.StreamBased),
					Guid.Empty
				) { RoutingResult = "finlandProxy/shadowsocks" }
			];
		}
	}


	public ObservableCollection<OnlineConnectionViewModel> Connections { get; } = [];
}
