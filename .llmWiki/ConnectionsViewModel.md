# ConnectionsViewModel

The `ConnectionsViewModel` is a key component in the GUI layer of the GammaRay project.

## Role and Responsibilities
Its primary role is to manage and provide a collection of active client connections to the User Interface. It acts as a data provider for displaying connection details (such as status, destination, and routing results) in the application's view.

## Class Details
- **File Path**: `GammaRay.Client.GUI\ViewModels\ConnectionsViewModel.cs`
- **Inheritance**: `ViewModelBase`
- **Properties**:
    - `Connections`: An `ObservableCollection<OnlineConnectionViewModel>` that holds the list of active or online connections.
- **Design Mode**: Includes a conditional block for `Design.IsDesignMode` that populates the collection with mock `OnlineConnectionViewModel` data for Avalonia designer previews.

## Usage
- **Integration**: It is instantiated as a property within `MainViewModel`.
- **View**: It is used by the `ConnectionsView.axaml` view.
