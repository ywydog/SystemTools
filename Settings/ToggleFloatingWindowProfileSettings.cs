using System.Text.Json.Serialization;

namespace SystemTools.Settings;

public class ToggleFloatingWindowProfileSettings
{
    [JsonPropertyName("targetProfileIndex")]
    public int TargetProfileIndex { get; set; } = -1;
}
