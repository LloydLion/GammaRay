using GammaRay.Core.Network;
using GammaRay.Core.Settings.Model;
using GammaRay.Core.Settings.Tree;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.Binders.Special;

public sealed class SettingsInternetAccessPointModelChannelBinder : SettingsTreeTypeBinder
{
	public override SettingsTreeBindResult Bind(SettingsTreeNode node, Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		if (
			node is not SettingsTreeMappingNode mappingNode ||
			mappingNode.Children.TryGetValue("uri", out var uriNode) ||
			uriNode is not SettingsTreeValueNode uriValueNode ||
			uriValueNode.Value is null
		)
			return SettingsTreeBindError.Single("Must be mapping node with not null valued 'uri' property", node);

		if (aggregateBinder.Bind(typeof(Uri), uriValueNode).TryI(out var error, out var result))
			return error;
		var uri = (Uri)result;
		
		var protocol = uri.Scheme;
		var endPoint = new GenericWebEndPoint(new WebHost(uri.Host), uri.Port);
		var parameters = uri.Query.TrimStart('?').Split('&').Select(s => s.Split('=')).ToDictionary(s => s[0], s => s[1]);

		var channel = new SettingsInternetAccessPointModel.Channel() { EndPoint = endPoint, Protocol = protocol, Parameters = new(parameters) };

		if (mappingNode.Children.TryGetValue("tags", out var tagsNode))
		{
			if (aggregateBinder.Bind(typeof(string[]), tagsNode).TryI(out error, out var channelTags))
				return error;
			channel.Tags = (string[])channelTags;
		}

		if (mappingNode.Children.TryGetValue("availableInNetwork", out var availableInNetworkNode))
		{
			if (aggregateBinder.Bind(typeof(string[]), availableInNetworkNode).TryI(out error, out var availableInNetwork))
				return error;
			channel.AvailableInNetwork = (string[])availableInNetwork;
		}

		return SettingsTreeBindResult.Success(channel);
	}

	public override bool CanBind(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type == typeof(SettingsInternetAccessPointModel.Channel);
	}
}
