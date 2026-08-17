using GammaRay.Core.Settings.Model;
using GammaRay.Core.Utils.FileSystem;
using Nito.AsyncEx;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.ValueParsers;

public sealed class SettingsLinkedFileValueParser(IFileSystemLocator _fileSystem) : ISettingsTreeValueParser
{
	public SettingsTreeValueParseResult TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder)
	{
		var path = new string(value);
		var content = AsyncContext.Run(() => _fileSystem.GetFileContentAsync(path).AsTask());
		
		if (content is null)
			return SettingsTreeValueParseResult.Failure($"File '{path}' not found");

		return SettingsTreeValueParseResult.Success(new SettingsLinkedFile(path, content));
	}

	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type == typeof(SettingsLinkedFile);
	}
}
