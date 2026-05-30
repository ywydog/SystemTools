using System.Text.Json.Serialization;

namespace SystemTools.Settings;

public class ToggleFloatingWindowLayerSettings
{
    [JsonPropertyName("targetLayer")]
    public int TargetLayer { get; set; } = -1;
}
