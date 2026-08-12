using GammaRay.Core.Settings.Model;
using GammaRay.Core.Utils.FileSystem;
using Nito.AsyncEx;
using System.Diagnostics.CodeAnalysis;

namespace GammaRay.Core.Settings.Binding.ValueParsers;

public sealed class SettingsLinkedFileValueParser(IFileSystemLocator _fileSystem) : ISettingsTreeValueParser
{
	public bool TryParse(Type type, ReadOnlySpan<char> value, SettingsTreeAggregateBinder aggregateBinder, [NotNullWhen(true)] out object? result)
	{
		result = null;
		var path = new string(value);
		var content = AsyncContext.Run(() => _fileSystem.GetFileContentAsync(path).AsTask());
		if (content is null)
			return false;

		result = new SettingsLinkedFile(path, content);
		return true;
	}

	public bool CanParse(Type type, SettingsTreeAggregateBinder aggregateBinder)
	{
		return type == typeof(SettingsLinkedFile);
	}
}
