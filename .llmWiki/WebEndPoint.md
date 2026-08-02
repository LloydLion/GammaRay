# WebEndPoint

`WebEndPoint` is a fundamental component in the **GammaRay** project, serving as a unified identifier for network-accessible resources.

## Definition

`WebEndPoint` is defined as a `readonly record struct` within the `GammaRay.Core.Network` namespace. It combines network addressing with transport layer information.

**File:** `GammaRay.Core\Network\WebEndPoint.cs`

```csharp
public readonly record struct WebEndPoint(GenericWebEndPoint GenericEndPoint, TransportType Protocol)
{
    public WebEndPoint(WebHost host, int port, TransportType protocol)
        : this(new GenericWebEndPoint(host, port), protocol)
    { }

    public WebHost Host => GenericEndPoint.Host;
    public int Port => GenericEndPoint.Port;

    public override string ToString()
    {
        return $"{Host}:{Port}/{Protocol}";
    }

    public static WebEndPoint Parse(string value, int defaultPort, TransportType protocol)
    {
        return new WebEndPoint(GenericWebEndPoint.Parse(value, defaultPort), protocol);
    }
}
```

### Components

1.  **`GenericWebEndPoint`**: A `readonly record struct` that encapsulates the physical address:
    *   `WebHost Host`: The hostname or IP address.
    *   `int Port`: The network port.
2.  **`TransportType`**: An enum specifying the communication protocol:
    *   `DatagramBased` (e.g., UDP)
    *   `StreamBased` (e.g., TCP)

## Usage and Role within GammaRay

`WebEndPoint` is used extensively across the architecture to represent "where" a service or connection resides and "how" to reach it.

### 1. Service Identification & Discovery
*   **`Service` Class**: Every discovered service in the network is identified by its `WebEndPoint` and its functional `Capability`.
*   **Service Repository**: The `DbServiceRepository` and `DbServiceStatusTableRepository` use the `WebEndPoint` (specifically the combination of Host, Port, and Protocol) as a primary key to uniquely identify and persist services.

### 2. Networking and Routing
*   **Channel Drivers**: `IChannelDriver` implementations (like `SOCKS5ChannelDriver` and `LocalChannelDriver`) use `WebEndPoint` as the `targetEndPoint` when attempting to open an `IAPChannel`.
*   **Routing**: The `RoutingRequest` struct uses `WebEndPoint` to define the destination for routed traffic.
*   **Inbound Drivers**: `HTTPInboundDriver` and `SOCKS5InboundDriver` instantiate `WebEndPoint` objects to represent the destination of incoming client requests.

### 3. Monitoring and Probing
*   **Probing System**: The `ProbingManager` and `HTTPProbingDriver` use `WebEndPoint` to target specific endpoints to verify service availability and health.
*   **Connection Tracking**: `OnlineConnection` and `ConnectionTrackingMonitoringSystem` use it to track and display active network connections in the TUI/GUI.

## Summary of Role
In GammaRay, `WebEndPoint` acts as the **universal network address**. By abstracting the physical address (Host/Port) and the transport method (Protocol) into a single type, it enables the project to treat diverse network resources (whether TCP-based web services or UDP-based datagram streams) through a consistent, type-safe interface for routing, discovery, and management.
