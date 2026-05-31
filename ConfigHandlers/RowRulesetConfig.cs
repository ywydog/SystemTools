using System.Text.Json.Serialization;
using ClassIsland.Core.Models.Ruleset;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.ConfigHandlers;

/// <summary>
/// 悬浮窗行的规则集配置
/// </summary>
public partial class RowRulesetConfig : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("isVisible")]
    private bool _isVisible = true;

    [ObservableProperty]
    [JsonPropertyName("rulesetEnabled")]
    private bool _rulesetEnabled = false;

    [JsonPropertyName("ruleset")]
    public Ruleset Ruleset { get; set; } = new();
}
