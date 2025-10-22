namespace GammaRay.Core.Settings;

using GammaRay.Core.Settings.Entities;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(ApplicationSettings))]
[JsonSerializable(typeof(ConfigurationSettings))]
[JsonSerializable(typeof(DomainCategorySettings))]
[JsonSerializable(typeof(InboundSettings))]
[JsonSerializable(typeof(NetworkProfileSettings))]
[JsonSerializable(typeof(RouteGridSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class SettingsJsonContext : JsonSerializerContext
{
}
