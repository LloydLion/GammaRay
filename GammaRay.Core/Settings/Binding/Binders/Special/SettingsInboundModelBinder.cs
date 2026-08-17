using GammaRay.Core.Settings.Model;
using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace GammaRay.Core.Settings.Binding.Binders.Special;

public sealed class SettingsInboundModelBinder : SettingsTreeTypeBinder
{
	public override SettingsTreeBindResult Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		if (aggregateBinder.Bind(typeof(Uri), node).TryI(out var error, out var result))
			return error;
		var uri = (Uri)result;
		
		var protocol = uri.Scheme;

		if (IPAddress.TryParse(uri.Host, out var ipAddress) == false)
			return SettingsTreeBindError.Single("Uri host must be valid IP address", node);
		
		var endPoint = new IPEndPoint(ipAddress, uri.Port);
		
		var model = new SettingsInboundModel() { Protocol = protocol, EndPoint = endPoint };
		return SettingsTreeBindResult.Success(model);
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type == typeof(SettingsInboundModel);
	}
}
