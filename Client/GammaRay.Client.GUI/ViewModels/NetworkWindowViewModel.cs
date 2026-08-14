using Avalonia.Controls;
using ReactiveUI;
using System.Diagnostics;
using System.Reactive;

namespace GammaRay.Client.GUI.ViewModels;

public sealed class NetworkWindowViewModel : ViewModelBase
{
	public NetworkWindowViewModel()
	{
		if (Design.IsDesignMode == false)
			Debug.Fail("Default constructor only for DESIGN mode");

		CurrentIdentity = "Eth0+AA:AA:AA:AA:AA:AA+192.168.11.22";
		CurrentProfile = "homeNetwork";

		ApplyChangesCommand = ReactiveCommand.Create(() => { });

		Mapping = [
			new NetworkProfileMappingViewModel("Eth0+AA:AA:AA:AA:AA:AA+192.168.11.22", "homeNetwork"),
			new NetworkProfileMappingViewModel("Eth1+AA:AA:AA:BB:BB:AA+191.128.11.33", ""),
			new NetworkProfileMappingViewModel("WIFI23+AA:AA:AA:CC:12:33+10.22.11.22", "default"),
		];
	}

	public NetworkWindowViewModel(
		IReadOnlyCollection<NetworkProfileMappingViewModel> mapping,
		string? currentIdentity,
		string? currentProfile,
		ReactiveCommand<Unit, Unit> applyChangesCommand
	)
	{
		Mapping = mapping;
		CurrentIdentity = currentIdentity ?? "No network";
		CurrentProfile = currentProfile ?? "No network";
		ApplyChangesCommand = applyChangesCommand;
	}


	public string CurrentIdentity { get; }

	public string CurrentProfile { get; }

	public ReactiveCommand<Unit, Unit> ApplyChangesCommand { get; }

	public IReadOnlyCollection<NetworkProfileMappingViewModel> Mapping { get; }
}

public sealed class NetworkProfileMappingViewModel : ViewModelBase
{
	public NetworkProfileMappingViewModel(string identity, string profile)
	{
		Identity = identity;
		Profile = profile;
	}


	public string Identity { get; }

	public string Profile 
	{
		get; 
		set
		{
			this.RaiseAndSetIfChanged(ref field, value);
			WasChanged = true;
		}
	}

	public bool WasChanged { get; set => this.RaiseAndSetIfChanged(ref field, value); }
}
