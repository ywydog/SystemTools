using System.Text.Json.Serialization;

namespace SystemTools.Settings;

/// <summary>
/// 切换悬浮窗配置方案行动的设置
/// </summary>
public class ToggleFloatingWindowProfileSettings
{
    /// <summary>
    /// 目标配置方案索引。-1 表示切换到下一个，其他值表示指定方案索引。
    /// </summary>
    [JsonPropertyName("targetProfileIndex")]
    public int TargetProfileIndex { get; set; } = -1;
}
