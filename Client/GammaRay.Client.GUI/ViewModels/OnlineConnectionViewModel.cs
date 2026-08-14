using ReactiveUI;
using System.Net;

namespace GammaRay.Client.GUI.ViewModels;

public sealed class OnlineConnectionViewModel(string inbound, IPEndPoint source, Guid id) : ViewModelBase
{
	public string Inbound { get; } = inbound;

	public IPEndPoint Source { get; } = source;

	public string? Destination { get; set => this.RaiseAndSetIfChanged(ref field, value); }

	public string? RoutingResult { get; set => this.RaiseAndSetIfChanged(ref field, value); }

	public string Status { get; set => this.RaiseAndSetIfChanged(ref field, value); } = "Blank";

	public Guid Id { get; } = id;
}
