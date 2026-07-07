using GammaRay.Core.Network;
using ReactiveUI;
using System.Net;

namespace GammaRay.Client.GUI.ViewModels;

public sealed class OnlineConnectionViewModel(string inbound, string inboundDriver, IPEndPoint endPoint, WebEndPoint destination, Guid id) : ViewModelBase
{
	public string InboundDriver { get; } = inboundDriver;

	public string Inbound { get; } = inbound;

	public IPEndPoint EndPoint { get; } = endPoint;

	public string Destination { get; } = $"{destination.Host}:{destination.Port}";

	public string? RoutingResult { get; set => this.RaiseAndSetIfChanged(ref field, value); } 

	public Guid Id { get; } = id;
}
