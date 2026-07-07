using ReactiveUI;
using System.Reactive.Linq;

namespace GammaRay.Client.GUI.ViewModels;

public sealed class ConnectServerWindowViewModel : ViewModelBase
{
	private readonly ObservableAsPropertyHelper<bool> _isValid;


	public ConnectServerWindowViewModel()
	{
		this.WhenAnyValue(x => x.HostName, x => x.Port)
			.Select(x => !string.IsNullOrWhiteSpace(x.Item1) && x.Item2 is > 0 and <= ushort.MaxValue)
			.ToProperty(this, x => x.IsValid, out _isValid);
	}


	public string HostName { get; set => this.RaiseAndSetIfChanged(ref field, value); } = string.Empty;

	public int Port { get; set => this.RaiseAndSetIfChanged(ref field, value); } = 0;

	public bool IsValid => _isValid.Value;
}
