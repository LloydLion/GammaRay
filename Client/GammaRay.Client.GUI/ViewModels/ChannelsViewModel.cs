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
				new IAPChannelStatusViewModel("usaProxy/vless+xhttp", "default", TimeSpan.FromSeconds(1.3), TimeSpan.FromSeconds(0.54), true, TimeSpan.FromMinutes(23)),
				new IAPChannelStatusViewModel("useProxy/shadowsocks", "default", TimeSpan.FromSeconds(1.1), TimeSpan.FromSeconds(0.88), true, TimeSpan.FromMinutes(44)),
				new IAPChannelStatusViewModel("finlandProxy/vless", "homeNet", TimeSpan.FromSeconds(1.9), TimeSpan.FromSeconds(0.52), false, TimeSpan.FromMinutes(123))
			];
		}
	}


	public ObservableCollection<IAPChannelStatusViewModel> Channels { get; } = [];
}
