using Avalonia;
using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using SystemTools.Rules;

namespace SystemTools.Controls;

public class UsbInsertedRuleSettingsControl : RuleSettingsControlBase<UsbInsertedRuleSettings>
{
    public UsbInsertedRuleSettingsControl()
    {
        Content = new TextBlock
        {
            Text = "检测当前是否有U盘处于插入状态。",
            Margin = new Thickness(10),
            TextWrapping = TextWrapping.Wrap
        };
    }
}