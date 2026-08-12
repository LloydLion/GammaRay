using GammaRay.Core.Settings.Model;
using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace GammaRay.Core.Settings.Binding.Binders.Special;

public sealed class SettingsInboundModelBinder : SettingsTreeTypeBinder
{
	public override bool Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result)
	{
		result = null;
		if (node is not SettingsTreeValueNode valueNode || valueNode.Value is null)
			return false;

		var uri = new Uri(valueNode.Value);
		var protocol = uri.Scheme;
		var endPoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);
		result = new SettingsInboundModel() { Protocol = protocol, EndPoint = endPoint };
		return true;
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type == typeof(SettingsInboundModel);
	}
}
