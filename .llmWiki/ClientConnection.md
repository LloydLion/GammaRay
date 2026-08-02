# ClientConnection

`ClientConnection` is a central component in the GammaRay project responsible for tracking and managing the complete lifecycle of a single client connection. It acts as both a state machine and a data container for all information related to a specific client session.

## Definition

Located in: `GammaRay.Core\Connection\ClientConnection.cs`

## Key Responsibilities

### 1. State Machine & Lifecycle Tracking
The class strictly enforces the connection lifecycle, ensuring that state transitions occur in the correct order (e.g., `Blank` $\rightarrow$ `Requested` $\rightarrow$ `Routed` $\rightarrow$ `Established`).

Common states include:
- **Requested**: Initial request received.
- **Routed**: A routing path has been determined.
- **Established**: The connection is active.
- **Rerouted**: The connection was redirected to a different channel.
- **Closed**: The connection has ended.

### 2. Data Container
It stores all relevant metadata for a connection, including:
- **Client Parameters**: `ClientNetworkParameters`
- **Request Info**: `ClientConnectionRequest`
- **Routing Results**: `NamedIAPChannel` (both initial and rerouting)
- **Establishment Info**: `ClientConnectionEstablishInfo`
- **Error Information**: `Exception` (if the connection fails)

### 3. Observability & Monitoring
By utilizing a `TrackableProcedure`, `ClientConnection` integrates with the project's `MonitoringSystem`, allowing the entire lifecycle of a connection to be recorded and observed.

## Architectural Integration

- **Orchestration**: The `MasterServer` (implementing `IMasterServer`) manages these connections, maintaining a collection of active `ClientConnection` objects and driving their state transitions.
- **Inbound Entry Points**: Drivers such as `SOCKS5InboundDriver` and `HTTPInboundDriver` initiate the lifecycle by creating connection requests.
- **Core Dependencies**:
    - `GammaRay.Core.Connection.Observation`
    - `GammaRay.Core.InternetAccess.Channels`
    - `GammaRay.Core.Monitoring`
    - `GammaRay.Core.Network.Flow`
