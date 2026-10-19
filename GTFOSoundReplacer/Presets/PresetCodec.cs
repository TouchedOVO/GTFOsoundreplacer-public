using System;
using System.Text.Json;

namespace GTFOSoundReplacer.Presets
{
    /// <summary>SoundBank 预设的统一 JSON 入口。</summary>
    public static class PresetCodec
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        public static string Serialize(PresetData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Entries == null) throw new JsonException("Preset entries must be an array.");
            return JsonSerializer.Serialize(data, Options);
        }

        public static PresetData Deserialize(string json)
        {
            // Validate compatibility metadata before deserializing into the current
            // model. Unknown JSON fields otherwise remain forward compatible.
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Preset must be an object.");
                bool hasEntries = false;
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "entries", StringComparison.OrdinalIgnoreCase)) continue;
                    hasEntries = true;
                    if (property.Value.ValueKind != JsonValueKind.Array)
                        throw new JsonException("Preset entries must be an array.");
                    foreach (JsonElement entry in property.Value.EnumerateArray())
                    {
                        if (entry.ValueKind == JsonValueKind.Null) continue;
                        if (entry.ValueKind != JsonValueKind.Object)
                            throw new JsonException("Preset entry must be an object.");
                        foreach (JsonProperty field in entry.EnumerateObject())
                        {
                            if (!string.Equals(field.Name, "sourceType", StringComparison.OrdinalIgnoreCase)) continue;
                            bool supported = field.Value.ValueKind == JsonValueKind.String
                                ? string.Equals(field.Value.GetString(), "SoundBank", StringComparison.OrdinalIgnoreCase)
                                : field.Value.ValueKind == JsonValueKind.Number &&
                                  field.Value.TryGetInt32(out int value) && value == 1;
                            if (!supported)
                                throw new JsonException("Preset contains an unsupported audio source.");
                        }
                    }
                }
                if (!hasEntries) throw new JsonException("Preset entries are required.");
            }
            PresetData data = JsonSerializer.Deserialize<PresetData>(json, Options);
            if (data == null || data.Entries == null)
                throw new JsonException("Preset entries must be an array.");
            return data;
        }
    }
}
