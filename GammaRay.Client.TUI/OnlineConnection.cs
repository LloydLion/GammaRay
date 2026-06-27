using GammaRay.Core.InternetAccess;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network;
using System.Net;

namespace GammaRay.Client.TUI;

public sealed class OnlineConnection(TrackableProcedure procedure, IPEndPoint endPoint, WebEndPoint destination, string inboundDriver)
{
	public TrackableProcedure Procedure { get; } = procedure;

	public IPEndPoint EndPoint { get; } = endPoint;

	public WebEndPoint Destination { get; } = destination;

	public string InboundDriver { get; } = inboundDriver;

	public (InternetAccessPoint IAP, string ChannelName)? RoutingResult { get; set; }

	public Status CurrentStatus { get; set; } = Status.Open;

	public Guid Id => Procedure.Id;

	public int TTL { get; set; } = 4;


	public enum Status
	{
		Open,
		Closed
	}
}
