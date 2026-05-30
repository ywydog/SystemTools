using System.Text.Json.Serialization;
using ClassIsland.Core.Models.Ruleset;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.ConfigHandlers;

public partial class ButtonRulesetConfig : ObservableRecipient
{
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private int _position = -1;
    [ObservableProperty] private bool _rulesetEnabled = false;
    [JsonPropertyName("ruleset")] public Ruleset Ruleset { get; set; } = new();
}
