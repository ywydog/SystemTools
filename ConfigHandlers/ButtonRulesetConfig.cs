using System.Text.Json.Serialization;
using ClassIsland.Core.Models.Ruleset;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.ConfigHandlers;

/// <summary>
/// 悬浮窗按钮的规则集配置
/// </summary>
public partial class ButtonRulesetConfig : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("isVisible")]
    private bool _isVisible = true;

    [ObservableProperty]
    [JsonPropertyName("position")]
    private int _position = -1;

    [ObservableProperty]
    [JsonPropertyName("rulesetEnabled")]
    private bool _rulesetEnabled = false;

    [JsonPropertyName("ruleset")]
    public Ruleset Ruleset { get; set; } = new();
}
