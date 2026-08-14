using Avalonia.Controls;
using GammaRay.Core.Services.Probing;
using System.Collections.ObjectModel;
using static GammaRay.Core.Services.Probing.ServiceIAPStatus;

namespace GammaRay.Client.GUI.ViewModels;

public sealed class ServicesViewModel
{
	public ServicesViewModel()
	{
		if (Design.IsDesignMode)
		{
			Services = [
				new FullServiceInfoViewModel(
					new(new("google.com"), 443), "HTTP",
					new Dictionary<string, ServiceIAPStatus>()
					{
						{ "finlandProxy", new(StatusType.Available, TimeSpan.FromSeconds(0.23)) },
						{ "usaProxy", new(StatusType.ServerSideBan, TimeSpan.FromSeconds(0.33)) },
						{ "local:default", new(StatusType.ServerSideBan, TimeSpan.FromSeconds(0.92)) }
					},
					TimeSpan.FromHours(4)
				),

				new FullServiceInfoViewModel(
					new(new("youtube.com"), 443), "HTTP",
					new Dictionary<string, ServiceIAPStatus>()
					{
						{ "finlandProxy", new(StatusType.ServerSideBan, TimeSpan.FromSeconds(0.23)) },
						{ "usaProxy", new(StatusType.ServerSideBan, TimeSpan.FromSeconds(0.13)) },
						{ "local:default", new(StatusType.Blocked, TimeSpan.FromSeconds(0)) }
					},
					TimeSpan.FromDays(1)
				),

				new FullServiceInfoViewModel(
					new(new("microsoft.com"), 443), "HTTP",
					new Dictionary<string, ServiceIAPStatus>()
					{
						{ "finlandProxy", new(StatusType.ServerSideBan, TimeSpan.FromSeconds(0.23)) },
						{ "usaProxy", new(StatusType.ServerSideBan, TimeSpan.FromSeconds(0.13)) },
						{ "local:default", new(StatusType.ServerSideBan, TimeSpan.FromSeconds(0.11)) }
					},
					TimeSpan.FromSeconds(1231)
				)
			];
		}
	}


	public ObservableCollection<FullServiceInfoViewModel> Services { get; } = [];
}
