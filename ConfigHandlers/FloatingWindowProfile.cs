using System.Collections.Generic;
using System.Text.Json.Serialization;
using ClassIsland.Core.Models.Ruleset;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.ConfigHandlers;

/// <summary>
/// 悬浮窗配置方案，保存一套完整的悬浮窗配置。
/// </summary>
public partial class FloatingWindowProfile : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("name")]
    private string _name = "Default";

    [ObservableProperty]
    [JsonPropertyName("showFloatingWindow")]
    private bool _showFloatingWindow = true;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowHorizontal")]
    private bool _floatingWindowHorizontal;

    [JsonPropertyName("floatingWindowButtonOrder")]
    public List<string> FloatingWindowButtonOrder { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRows")]
    public List<List<string>> FloatingWindowButtonRows { get; set; } = new();

    [ObservableProperty]
    [JsonPropertyName("floatingWindowScale")]
    private double _floatingWindowScale = 1.0;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowIconSize")]
    private int _floatingWindowIconSize = 22;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowTextSize")]
    private int _floatingWindowTextSize = 12;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowOpacity")]
    private int _floatingWindowOpacity = 80;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowPositionX")]
    private int _floatingWindowPositionX = 100;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowPositionY")]
    private int _floatingWindowPositionY = 100;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowLayer")]
    private int _floatingWindowLayer = 1;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowLayerRecheckMode")]
    private int _floatingWindowLayerRecheckMode = 1;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowShadowEnabled")]
    private bool _floatingWindowShadowEnabled = true;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowDragHandleAlwaysVisible")]
    private bool _floatingWindowDragHandleAlwaysVisible;

    [ObservableProperty]
    [JsonPropertyName("floatingWindowRulesetEnabled")]
    private bool _floatingWindowRulesetEnabled;

    [JsonPropertyName("floatingWindowRuleset")]
    public Ruleset FloatingWindowRuleset { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRulesets")]
    public Dictionary<string, ButtonRulesetConfig> FloatingWindowButtonRulesets { get; set; } = new();

    [JsonPropertyName("floatingWindowRowRulesets")]
    public List<RowRulesetConfig> FloatingWindowRowRulesets { get; set; } = new();
}
