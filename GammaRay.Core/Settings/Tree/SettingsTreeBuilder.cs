using System.Collections.Frozen;

namespace GammaRay.Core.Settings.Tree;

public abstract class SettingsTreeBuilder
{
	public abstract SettingsTreeNode Build();
}

public sealed class SettingsTreeValueNodeBuilder(string? _value) : SettingsTreeBuilder
{
	public override SettingsTreeNode Build() => new SettingsTreeValueNode(_value);
}

public sealed class SettingsTreeListNodeBuilder : SettingsTreeBuilder
{
	private readonly List<SettingsTreeBuilder> _children = new();


	public void Add(SettingsTreeBuilder value) => _children.Add(value);

	public override SettingsTreeListNode Build() => new(_children.Select(c => c.Build()).ToArray());
}

public sealed class SettingsTreeMappingNodeBuilder : SettingsTreeBuilder
{
	private readonly Dictionary<string, SettingsTreeBuilder> _children = new();


	public void Add(string key, SettingsTreeBuilder value) => _children.Add(key, value);

	public override SettingsTreeMappingNode Build() => new(_children.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.Build())).ToFrozenDictionary());
}

public static class SettingsTreeBuilderExtensions
{
	extension(SettingsTreeListNodeBuilder builder)
	{
		public void Add(string value) => builder.Add(new SettingsTreeValueNodeBuilder(value));
	}

	extension(SettingsTreeMappingNodeBuilder builder)
	{
		public void Add(string key, string value) => builder.Add(key, new SettingsTreeValueNodeBuilder(value));
	}
}
