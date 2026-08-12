using GammaRay.Core.Utils.FileSystem;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using System.Diagnostics;
using YamlDotNet.RepresentationModel;

namespace GammaRay.Core.Settings.Tree.Loading;

public sealed class YAMLSettingsTreeLoader: ISettingsTreeLoader
{
	public SettingsTree LoadTree(string sourceSettingsFileContent)
	{
		var stream = new YamlStream();
		stream.Load(new StringReader(sourceSettingsFileContent));
		var document = stream.Documents[0];

		var visitor = new Visitor();
		document.Accept(visitor);

		return new SettingsTree(visitor.Builder.Build());
	}


	private class Visitor : IYamlVisitor
	{
		public SettingsTreeBuilder Builder { get => field ?? throw new UnreachableException(); private set; }


		public void Visit(YamlScalarNode scalar)
		{
			Builder = new SettingsTreeValueNodeBuilder(scalar.Value!);
		}

		public void Visit(YamlSequenceNode sequence)
		{
			var builder = new SettingsTreeListNodeBuilder();
			foreach (var child in sequence.Children)
			{
				child.Accept(this);
				builder.Add(Builder);
			}
			Builder = builder;
		}

		public void Visit(YamlMappingNode mapping)
		{
			var builder = new SettingsTreeMappingNodeBuilder();
			foreach (var (key, value) in mapping.Children)
			{
				if (key is not YamlScalarNode scalarKey)
					continue;

				var keyValue = scalarKey.Value ?? throw new UnreachableException();

				value.Accept(this);
				builder.Add(keyValue, Builder);
			}
			Builder = builder;
		}

		public void Visit(YamlDocument document) => document.RootNode.Accept(this);

		public void Visit(YamlStream stream) => throw new NotSupportedException();
	}
}
