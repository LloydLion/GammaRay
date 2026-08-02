# ClientConnectionState

`ClientConnectionState` is an enumeration that defines the valid stages in a client connection's lifecycle. It is used within the `ClientConnection` class to manage and validate the state transitions of a connection.

## Definition
The enum is defined in `GammaRay.Core\Connection\ClientConnectionState.cs`:

```csharp
public enum ClientConnectionState
{
    Blank = 0,
    Requested = 1,
    Routed = 2,
    Established = 3,

    ClosedByClient = 4,
    ClosedByRemote = 5,
    Rerouted = 6
}
```

It also includes an extension method to check if a state is considered "closed":
```csharp
public static class ClientConnectionStateExtensions
{
    public static bool IsClosed(this ClientConnectionState state) => state is ClientConnectionState.ClosedByClient
        or ClientConnectionState.ClosedByRemote
        or ClientConnectionState.Rerouted;
}
```

## Role and Usage
The primary role of this enum is to act as the state indicator for the `ClientConnection` class (`GammaRay.Core\Connection\ClientConnection.cs`).

### State Management and Validation
The `ClientConnection` class uses the state to enforce a strict lifecycle via the `RequireState` method. This ensures that operations only occur when the connection is in the expected state. For example:
* **`AddRequest`**: Requires the state to be `Blank` and moves it to `Requested`.
* **`AddRoute`**: Requires the state to be `Requested` and moves it to `Routed`.
* **`Establish`**: Requires the state to be `Routed` and moves it to `Established`.
* **`CloseByClient` / `CloseByRemote`**: Requires the state to be `Established`.

### State Querying
The class provides several boolean properties that use relational patterns to query the current state:
* `WasRequested`: `State is >= ClientConnectionState.Requested`
* `WasRouted`: `State is >= ClientConnectionState.Routed`
* `WasEstablished`: `State is >= ClientConnectionState.Established`
* `IsRerouted`: `State is ClientConnectionState.Rerouted`
* `IsClosed`: Uses the extension property `State.IsClosed`.

## Lifecycle Stages Summary
| State | Description |
| :--- | :--- |
| `Blank` | Initial state, no connection activity started. |
| `Requested` | A connection request has been initiated. |
| `Routed` | A network route/channel has been assigned to the request. |
| `Established` | The connection is active and fully operational. |
| `ClosedByClient` | The connection was terminated by the local client. |
| `ClosedByRemote` | The connection was terminated by the remote endpoint. |
| `Rerouted` | The connection was moved to a different route/channel. |

## Related Files
- [GammaRay.Core\Connection\ClientConnectionState.cs](GammaRay.Core\Connection\ClientConnectionState.cs)
- [GammaRay.Core\Connection\ClientConnection.cs](GammaRay.Core\Connection\ClientConnection.cs)
