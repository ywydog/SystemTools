using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ClassIsland.Core.Models.Ruleset;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.ConfigHandlers;

public partial class FloatingWindowProfile : ObservableObject
{
    [ObservableProperty] private string _name = "未命名方案";

    [ObservableProperty] private bool _showFloatingWindow = true;

    [ObservableProperty] private bool _floatingWindowHorizontal;

    [JsonPropertyName("floatingWindowButtonOrder")]
    public List<string> FloatingWindowButtonOrder { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRows")]
    public List<List<string>> FloatingWindowButtonRows { get; set; } = new();

    [ObservableProperty] private double _floatingWindowScale = 1.0;

    [ObservableProperty] private int _floatingWindowPositionX = 100;

    [ObservableProperty] private int _floatingWindowPositionY = 100;

    [ObservableProperty] private int _floatingWindowLayer = 1;

    [ObservableProperty] private int _floatingWindowLayerRecheckMode = 1;

    [ObservableProperty] private bool _floatingWindowShadowEnabled = true;

    [ObservableProperty] private bool _floatingWindowDragHandleAlwaysVisible;

    [ObservableProperty] private bool _floatingWindowRulesetEnabled;

    [JsonPropertyName("floatingWindowRuleset")]
    public Ruleset FloatingWindowRuleset { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRulesets")]
    public Dictionary<string, ButtonRulesetConfig> FloatingWindowButtonRulesets { get; set; } = new();
}
