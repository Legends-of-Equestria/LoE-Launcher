using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LoE_Launcher.Services;
using Models;

namespace LoE_Launcher.Core;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(LauncherVersionInfo))]
[JsonSerializable(typeof(VersionsControlFile))]
[JsonSerializable(typeof(MainControlFile))]
[JsonSerializable(typeof(Dictionary<string, FileHashCache>))]
[JsonSerializable(typeof(List<string>))]
public partial class LauncherJson : JsonSerializerContext
{
    public static T? Parse<T>(string json, JsonTypeInfo<T> type) =>
        JsonSerializer.Deserialize(json.TrimStart('\uFEFF'), type);
}
