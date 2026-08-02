# GammaRay Architecture Overview

GammaRay implements a **Client-Server architecture** designed for intelligent network routing and service monitoring. It follows a clear separation of concerns between core business logic, platform-specific implementations, backend services, and a graphical user interface.

## Architectural Overview

*   **The Server (`GammaRay.Service`)**: Acts as a central hub. It uses the `MasterServer` to listen for inbound connections, decides how to route them using a routing engine, and manages the lifecycle of these connections by bridging them to various internet access channels.
*   **The Client (`GammaRay.Client.GUI`)**: A monitoring dashboard that connects to the server via an API. It provides real-time visibility into the server's state, including active connections, available internet access channels, and discovered services.
*   **The Core (`GammaRay.Core`)**: The shared foundation. It contains the fundamental primitives for networking, the logic for routing requests, the definitions of services and capabilities, and the protocols used for communication.

## Modules

| Module | Role & Responsibility |
| :--- | :--- |
| **`GammaRay.Core`** | The foundational library containing the shared logic, interfaces, and data structures used by both the server and the client (e.g., networking, routing, and service definitions). |
| **`GammaRay.Core.Windows`** | A specialized module containing Windows-specific implementations or extensions for the core logic. |
| **`GammaRay.Service`** | The server-side implementation that executes the core logic, hosting the `MasterServer` and managing inbound traffic. |
| **`GammaRay.Client.GUI`** | The Avalonia-based client application used by operators to monitor and manage the GammaRay system. |
| **`InternetAccess`** | (Found in `GammaRay.Core.InternetAccess`) Manages the various pathways (channels) available to reach the internet. |
| **`Connection`** | (Found in `GammaRay.Core.Connection`) Manages the lifecycle, state transitions, and health monitoring of client-to-server connections. |
| **`Protocols`** | (Found in `GammaRay.Core.Protocols`) Contains the implementation of various communication protocols (e.g., SOCKS5, HTTP). |
| **`Services`** | (Found in `GammaRay.Core.Services`) Handles the definition, discovery, and probing of functional services available in the network. |
| **`Network`** | (Found in `GammaRay.Core.Network`) Provides the low-level primitives for addressing and transport (e.g., `WebEndPoint`, `TransportType`). |
| **`Monitoring & Routing`** | (Found in `GammaRay.Core.Monitoring` and `GammaRay.Core.Routing`) The "brain" of the system; handles the intelligent decision-making for request paths and tracks the health of the entire network. |

## Key Classes

### Internet Access & Networking
*   **`IAPChannel`**: Represents a specific communication channel to the internet. It is defined by a `DriverName` (which identifies the implementation) and an `EndPoint`.
*   **`InternetAccessPoint`**: A logical grouping or "gateway" that contains a collection of `IAPChannel`s.
*   **`IChannelDriver`**: An interface for drivers capable of opening an `IAPChannel` to a specific `WebEndPoint`.
*   **`WebHost`**: A lightweight wrapper for a domain or hostname.
*   **`WebEndPoint`**: A composite identifier consisting of a `WebHost`, a port, and a `TransportType`.
*   **`TransportType`**: An enumeration specifying the transport protocol used (e.g., `StreamBased`).

### Connection Management
*   **`MasterServer`**: The central orchestrator on the server. It manages inbound agents, uses a router to make decisions, instructs drivers to open channels, and monitors connections for "staleness" (e.g., low throughput) to trigger rerouting.
*   **`ClientConnection`**: Tracks the complete lifecycle of a single client connection, moving through states like `Requested` $\rightarrow$ `Routed` $\rightarrow$ `Established` $\rightarrow$ `Rerouted` or `Closed`.
*   **`ClientConnectionState`**: An enumeration defining the valid stages in a connection's lifecycle.

### Service Discovery & Capabilities
*   **`Service`**: Represents a functional service discovered in the network, identified by its `WebEndPoint` and its functional `Capability`.
*   **`ICapabilityDetector`**: An interface for components that analyze a `RoutingRequest` to determine what specific `Capability` is being sought.
*   **`CapabilityClassProvider`**: A registry that manages and provides lookups for all available `CapabilityClass` definitions loaded from the system configuration.

### Client GUI (MVVM)
*   **`MainViewModel`**: The root ViewModel for the client. It manages the connection to the server and orchestrates the child ViewModels.
*   **`ServicesViewModel`**: Manages the collection of services discovered by the server for display in the UI.
*   **`ConnectionsViewModel`**: Manages the collection of active client connections for display in the UI.
*   **`MainWindow`**: The primary top-level window of the client application (Avalonia-based). It serves as the main dashboard, hosting the `ConnectionsView`, `ChannelsView`, and `ServicesView` within a central grid. It also provides a menu for managing server connections and accessing the network management window, orchestrated via the `MainViewModel`.
*   **`ServicesView` / `ConnectionsView`**: The UI components (Views) responsible for rendering the service and connection lists, respectively.
