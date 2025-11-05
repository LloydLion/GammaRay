using GammaRay.Core.Network;
using GammaRay.Core.Windows.Management;
using Serilog;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaRay.Core.Windows.Network;


public partial class WindowsNetProfileBasedNetworkIdentifier(PowerShellHost _powerShell) : NetworkIdentifierBase(OSPlatform.Windows, _logger)
{
	private static readonly ILogger _logger = Log.ForContext<WindowsNetProfileBasedNetworkIdentifier>();


	protected override NetworkIdentity FetchCurrentNetworkIdentity()
	{
		var internetInterfaceIP = TraceRouteToInternet();
		var internetInterface = GetInterfaceByIP(internetInterfaceIP);
		var profileId = GetWindowsNetworkProfileForInterface(internetInterface);
		return new NetworkIdentity([profileId.ToString()]);

	}

	private string GetWindowsNetworkProfileForInterface(NetworkInterface networkInterface)
	{
		var interfaceIndex = networkInterface.GetIPProperties().GetIPv4Properties().Index;

		var results = _powerShell.RunCommand(
			$$"""
			Get-NetConnectionProfile | where {$_.InterfaceIndex -eq {{interfaceIndex}}} | select InstanceId | ConvertTo-Json -Compress
			"""
		).ToArray();

		if (results is not [var singleElement])
			goto error;
			
		var model = JsonSerializer.Deserialize(singleElement, JsonContext.Default.PSReturnModel);
		if (model is null or { InstanceId: null or "" })
			goto error;

		return model.InstanceId;

		error:
			throw new Exception($"No Windows network profile for \"{networkInterface.Name}\"");
	}


	private record PSReturnModel(string InstanceId);

	[JsonSerializable(typeof(PSReturnModel))]
	private partial class JsonContext : JsonSerializerContext;
}
