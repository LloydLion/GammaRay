# ServicesView

The `ServicesView` is an Avalonia UI component within the GammaRay project designed to display a list of discovered services and their current operational status.

## Implementation
- **Type**: `UserControl`
- **UI Definition**: `GammaRay.Client.GUI\Views\ServicesView.axaml`
- **Code-behind**: `GammaRay.Client.GUI\Views\ServicesView.axaml.cs`

## Functionality
The view utilizes an Avalonia `DataGrid` to present service information. The grid includes the following columns:
- **EndPoint**: The host and port of the service.
- **Class**: The capability class of the service.
- **Remaining**: The remaining time until the service status decays.
- **Status table**: A visual representation of the service's status across various probes/proxies.

## Data Binding and Architecture
- **ViewModel**: Binds to `ServicesViewModel`.
- **Data Source**: The `ServicesViewModel` contains an `ObservableCollection<FullServiceInfoViewModel>` which is populated and updated by a `ServerStateObserver`.
- **Update Mechanism**: The `ServerStateObserver` periodically queries the `GammaRayAPIClient` to fetch the latest service information.
- **Integration**: Embedded in the main application window via `MainWindow.axaml`.

## Related Files
- `GammaRay.Client.GUI\ViewModels\ServicesViewModel.cs`
- `GammaRay.Client.GUI\ViewModels\MainViewModel.cs`
- `GammaRay.Client.GUI\Views\MainWindow.axaml`
