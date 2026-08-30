using System.Text.Json.Serialization;

namespace Highbyte.DotNet6502.Impl.Avalonia.Oric;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(OricHostConfig))]
internal partial class OricHostConfigJsonContext : JsonSerializerContext { }
