using GammaRay.Core.Settings.Binding.Binders;
using GammaRay.Core.Settings.Binding.Binders.Special;
using GammaRay.Core.Settings.Binding.ValueParsers;
using GammaRay.Core.Utils.FileSystem;

namespace GammaRay.Core.Settings.Binding;

public static class SettingsTreeAggregateBinderSource
{
	public static SettingsTreeAggregateBinder Create(IFileSystemLocator fileSystem)
	{
		return new SettingsTreeAggregateBinder(
			[
				new SettingsInboundModelBinder(),
				new SettingsInternetAccessPointModelChannelBinder(),

				new ArrayBinder(),
				new SDBinder(),
				new CommonObjectBinder(),
				new ParsePrimitiveBinder()
			],

			[
				new TryParseMethodBasedSettingsTreeValueParser(),
				new StringSettingsTreeValueParser(),
				new EnumSettingsTreeValueParser(),
				new ValueConditionValueParser(),
				new UriTreeValueParser(),
				new SettingsLinkedFileValueParser(fileSystem)
			]
		);
	}
}
