using System.Text.Json.Serialization;

namespace SystemTools.Settings;

/// <summary>
/// "开关插件"行动设置。
/// </summary>
public class PluginToggleActionSettings
{
    /// <summary>
    /// 要操作的插件 ID（即 manifest 中的 id）。
    /// </summary>
    [JsonPropertyName("pluginId")]
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// 操作类型：切换、启用、禁用。
    /// </summary>
    [JsonPropertyName("operation")]
    public PluginToggleOperation Operation { get; set; } = PluginToggleOperation.Toggle;

    /// <summary>
    /// 变更后是否立刻重启 ClassIsland 以应用启用/禁用。
    /// </summary>
    [JsonPropertyName("restartImmediately")]
    public bool RestartImmediately { get; set; } = true;

    /// <summary>
    /// 是否静默重启。关闭主窗口、不弹窗提示。
    /// </summary>
    [JsonPropertyName("quietRestart")]
    public bool QuietRestart { get; set; } = false;
}

/// <summary>
/// "开关插件"行动的操作类型。
/// </summary>
public enum PluginToggleOperation
{
    /// <summary>
    /// 切换：根据当前状态取反。
    /// </summary>
    Toggle = 0,

    /// <summary>
    /// 强制启用。
    /// </summary>
    Enable = 1,

    /// <summary>
    /// 强制禁用。
    /// </summary>
    Disable = 2,
}
