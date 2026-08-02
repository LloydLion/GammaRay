# IChannelDriver

The `IChannelDriver` interface is a core component of the GammaRay project's internet access abstraction layer. It is used to decouple the logic of *how* a network channel is opened from the logic of *what* is done once the channel is open (e.g., probing or data transmission).

## Definition
Defined in `GammaRay.Core\InternetAccess\Channels\IChannelDriver.cs`.

```csharp
namespace GammaRay.Core.InternetAccess.Channels;

public interface IChannelDriver
{
    public ValueTask<ChannelOpeningResult> TryOpenChannelAsync(IAPChannel channel, WebEndPoint targetEndPoint);
}
```

The method returns a `ChannelOpeningResult` (a `readonly struct`), which encapsulates the outcome:
- **Success**: Provides an `IOpenChannel` instance.
- **ConnectionError**: Indicates a failure to establish the connection.
- **Exception**: Indicates an unexpected error during the attempt.

## Implementations
*   **`SOCKS5ChannelDriver`**: Handles establishing connections through a SOCKS5 proxy.
*   **`LocalChannelDriver`**: Handles connections that are local or do not require external proxying.

## Usage
*   **`ProbingManager`**: Retrieves the appropriate driver from an `IChannelDriverRegistry` and calls `TryOpenChannelAsync`.
*   **`MasterServer`**: Uses an `IChannelDriverRegistry` to manage available drivers.
*   **Dependency Injection**: Drivers are registered in `Program.cs`.

## Purpose
Provides a **pluggable mechanism for establishing network connectivity**, allowing higher-level services to request connections without knowing the underlying protocol or proxy requirements.
