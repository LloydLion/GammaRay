using GammaRay.Core.Connection.Observation;
using GammaRay.Core.InternetAccess.Channels;
using GammaRay.Core.Monitoring;
using GammaRay.Core.Network.Flow;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Connection;

public sealed class ClientConnectionEstablishInfo(
	ConnectionObserver observer,
	FlowJoinTask joinTask,
	IOpenChannel openChannel,
	ConnectionObservationRow observationRow,
	DateTime establishTime
)
{
	public ConnectionObserver Observer { get; } = observer;

	public FlowJoinTask JoinTask { get; } = joinTask;

	public IOpenChannel OpenChannel { get; } = openChannel;

	public ConnectionObservationRow ObservationRow { get; } = observationRow;

	public DateTime EstablishTime { get; } = establishTime;
}

public sealed class ClientConnection
{
	public ClientConnection(ClientNetworkParameters client, MonitoringSystem monitoring, Guid id, DateTime now)
	{
		Procedure = TrackableProcedure.New("Connection", now, monitoring, id);
		Client = client;
	}


	public ClientConnectionState State { get; private set; } = ClientConnectionState.Blank;

	[MemberNotNullWhen(true, nameof(Request))]
	public bool WasRequested => State is >= ClientConnectionState.Requested;

	[MemberNotNullWhen(true, nameof(Request))]
	[MemberNotNullWhen(true, nameof(RoutingResult))]
	public bool WasRouted => State is >= ClientConnectionState.Routed;

	[MemberNotNullWhen(true, nameof(Request))]
	[MemberNotNullWhen(true, nameof(RoutingResult))]
	[MemberNotNullWhen(true, nameof(EstablishInfo))]
	public bool WasEstablished => State is >= ClientConnectionState.Established;

	[MemberNotNullWhen(true, nameof(Request))]
	[MemberNotNullWhen(true, nameof(RoutingResult))]
	[MemberNotNullWhen(true, nameof(EstablishInfo))]
	[MemberNotNullWhen(true, nameof(ReroutingResult))]
	public bool IsRerouted => State is ClientConnectionState.Rerouted;

	public bool IsClosed => State.IsClosed;

	public bool IsStale { get; private set; }

	public bool IsErrored => Exception is not null;

	public ClientNetworkParameters Client { get; }

	public Guid Id => Procedure.Id;

	public DateTime CreationTime => Procedure.CreationTime;

	public TrackableProcedure Procedure { get; }

	public ClientConnectionRequest? Request { get; private set; }

	public NamedIAPChannel? RoutingResult { get; private set; }

	public ClientConnectionEstablishInfo? EstablishInfo { get; private set; }

	public NamedIAPChannel? ReroutingResult { get; private set; }

	public Exception? Exception { get; private set; }


	public void AddRequest(ClientConnectionRequest request)
	{
		RequireState(ClientConnectionState.Blank);
		Request = request;
		State = ClientConnectionState.Requested;
	}

	public void AddRoute(NamedIAPChannel routingResult)
	{
		RequireState(ClientConnectionState.Requested);
		RoutingResult = routingResult;
		State = ClientConnectionState.Routed;
	}

	public void Establish(ClientConnectionEstablishInfo establishInfo)
	{
		RequireState(ClientConnectionState.Routed);
		EstablishInfo = establishInfo;
		State = ClientConnectionState.Established;
	}

	public void DefineAsStale(bool stale)
	{
		RequireState(ClientConnectionState.Established);
		IsStale = stale;
	}

	public void CloseByClient()
	{
		RequireState(ClientConnectionState.Established);
		State = ClientConnectionState.ClosedByClient;
		Procedure.Finish();
	}

	public void CloseByRemote()
	{
		RequireState(ClientConnectionState.Established);
		State = ClientConnectionState.ClosedByRemote;
		Procedure.Finish();
	}

	public void Reroute(NamedIAPChannel routingResult)
	{
		RequireState(ClientConnectionState.Established);
		ReroutingResult = routingResult;
		State = ClientConnectionState.Rerouted;
		Procedure.Finish();
	}

	public void MarkAsErrored(Exception exception)
	{
		Exception = exception;
		Procedure.SetFatalException(exception);
		Procedure.Finish();
	}

	private void RequireState(ClientConnectionState state) => Debug.Assert(State == state);
}
