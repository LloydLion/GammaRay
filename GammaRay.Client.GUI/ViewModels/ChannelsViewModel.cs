using Avalonia.Controls;
using System.Collections.ObjectModel;

namespace GammaRay.Client.GUI.ViewModels;

public sealed class ChannelsViewModel : ViewModelBase
{
	public ChannelsViewModel()
	{
		if (Design.IsDesignMode)
		{
			Channels = [
				new IAPChannelStatusViewModel("usaProxy/vless+xhttp", "default", TimeSpan.FromSeconds(1.3), true),
				new IAPChannelStatusViewModel("useProxy/shadowsocks", "default", TimeSpan.FromSeconds(1.1), true),
				new IAPChannelStatusViewModel("finlandProxy/vless", "homeNet", TimeSpan.FromSeconds(1.9), false)
			];
		}
	}


	public ObservableCollection<IAPChannelStatusViewModel> Channels { get; } = [];
}
