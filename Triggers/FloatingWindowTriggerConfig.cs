using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SystemTools.Triggers;

public partial class FloatingWindowTriggerConfig : ObservableRecipient
{
    [ObservableProperty] private string _buttonId = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _icon = "/uEA37";
    [ObservableProperty] private string _buttonName = "触发按钮 1";
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private int _position = -1;
}
