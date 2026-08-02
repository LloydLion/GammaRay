# ICapabilityDetector

`ICapabilityDetector` is an interface used in the GammaRay project for **Service Discovery & Capabilities**. Its primary role is to analyze incoming `RoutingRequest` objects to determine the specific `Capability` (the functional nature of the service) being sought.

## Definition
The interface is defined in `GammaRay.Core/Services/ICapabilityDetector.cs`.

```csharp
public interface ICapabilityDetector
{
    public Capability Detect(RoutingRequest request);
}
```

## Implementations
The project provides a default implementation: **`DefaultCapabilityDetector`** (`GammaRay.Core/Services/DefaultCapabilityDetector.cs`).

* **Mechanism**: It uses a `CapabilityClassProvider` to iterate through available `CapabilityClass` definitions.
* **Detection Logic**: It performs a rule-based check (`PerformBasicRuleCheck`) by comparing the `RoutingRequest.Destination.Protocol` and `Port` against the `DetectionRules` defined for each capability class.
* **Fallback**: If no rules match, it falls back to a default capability class (the last one in the provider's list).

## Usage in the Project
The most critical usage of `ICapabilityDetector` is within the **`SmartRouter`** (`GammaRay.Core/Routing/SmartRouter.cs`) during the routing decision process.

When `SmartRouter.MakeRoutingDecision(RoutingRequest request)` is called:
1. The router checks if a valid `Service` for the destination is already known via the `_serviceRepository`.
2. If no service exists (or the existing one has expired), the router calls `_capabilityDetector.Detect(request)` to identify the service's capability.
3. A new `Service` object is then instantiated using this detected capability and registered in the repository.

## Summary of Role
In the context of the GammaRay architecture, `ICapabilityDetector` acts as a classification engine. It allows the system to transition from a raw network request (destination IP/Port/Protocol) to a high-level "Service" object that possesses specific functional properties, enabling more intelligent routing and monitoring decisions.

## Related Files
- `GammaRay.Core/Services/ICapabilityDetector.cs`
- `GammaRay.Core/Services/DefaultCapabilityDetector.cs`
- `GammaRay.Core/Routing/SmartRouter.cs`
