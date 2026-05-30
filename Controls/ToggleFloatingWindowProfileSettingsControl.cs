using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Controls.Primitives;
using ClassIsland.Core.Abstractions.Controls;
using SystemTools.Settings;
using SystemTools.ConfigHandlers;
using ClassIsland.Shared;

namespace SystemTools.Controls;

public class ToggleFloatingWindowProfileSettingsControl : ActionSettingsControlBase<ToggleFloatingWindowProfileSettings>
{
    private ComboBox _profileComboBox;

    public ToggleFloatingWindowProfileSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "目标配置方案:",
            FontWeight = Avalonia.Media.FontWeight.Bold
        });

        _profileComboBox = new ComboBox
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };

        LoadProfiles();

        panel.Children.Add(_profileComboBox);

        panel.Children.Add(new TextBlock
        {
            Text = "提示：选择\"切换到下一个\"会按顺序循环切换方案，选择具体方案会直接切换到该方案。",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 12
        });

        Content = panel;
    }

    private void LoadProfiles()
    {
        _profileComboBox.Items.Clear();
        _profileComboBox.Items.Add(new ComboBoxItem { Content = "切换到下一个", Tag = -1 });

        try
        {
            var configHandler = IAppHost.GetService<MainConfigHandler>();
            var profiles = configHandler.Data.FloatingWindowProfiles;

            for (int i = 0; i < profiles.Count; i++)
            {
                _profileComboBox.Items.Add(new ComboBoxItem
                {
                    Content = profiles[i].Name,
                    Tag = i
                });
            }
        }
        catch
        {
            // 服务可能尚未初始化
        }

        _profileComboBox.SelectedIndex = 0;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _profileComboBox.SelectionChanged += OnProfileSelectionChanged;

        RestoreSettings();
    }

    private void RestoreSettings()
    {
        if (Settings == null) return;

        var targetIndex = Settings.TargetProfileIndex;
        if (targetIndex >= 0 && targetIndex + 1 < _profileComboBox.Items.Count)
        {
            _profileComboBox.SelectedIndex = targetIndex + 1;
        }
        else
        {
            _profileComboBox.SelectedIndex = 0;
        }
    }

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_profileComboBox.SelectedItem is ComboBoxItem item && item.Tag is int index)
        {
            Settings.TargetProfileIndex = index;
        }
    }
}
