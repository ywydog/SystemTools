using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.Plugin;
using SystemTools.Settings;

namespace SystemTools.Controls;

/// <summary>
/// "开关插件"行动设置控件。
/// </summary>
public class PluginToggleActionSettingsControl : ActionSettingsControlBase<PluginToggleActionSettings>
{
    private readonly ComboBox _pluginComboBox = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly ComboBox _operationComboBox = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly CheckBox _restartImmediatelyCheckBox = new()
    {
        Content = "变更后立刻重启 ClassIsland 以应用",
        IsChecked = true
    };
    private readonly CheckBox _quietRestartCheckBox = new()
    {
        Content = "静默重启（不弹窗确认）",
        Margin = new(24, 0, 0, 0)
    };
    private readonly TextBlock _infoTextBlock = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.Gray,
        FontSize = 12,
        Margin = new(0, 4, 0, 0),
        IsVisible = false
    };
    private List<PluginInfo> _plugins = new();

    public PluginToggleActionSettingsControl()
    {
        var panel = new StackPanel { Spacing = 6, Margin = new(10) };

        // 目标插件
        var pluginRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto")
        };
        pluginRow.Children.Add(new TextBlock
        {
            Text = "目标插件：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 0, 6, 0)
        });
        Grid.SetColumn(pluginRow.Children[^1], 0);

        pluginRow.Children.Add(_pluginComboBox);
        Grid.SetColumn(_pluginComboBox, 1);
        _pluginComboBox.SelectionChanged += (_, _) =>
        {
            if (_pluginComboBox.SelectedItem is PluginInfo info)
            {
                Settings.PluginId = info.Manifest.Id;
            }
        };

        var refreshButton = new Button
        {
            Content = "刷新",
            Margin = new(6, 0, 0, 0)
        };
        refreshButton.Click += (_, _) => RefreshPluginList();
        pluginRow.Children.Add(refreshButton);
        Grid.SetColumn(refreshButton, 2);
        panel.Children.Add(pluginRow);

        // 操作
        var operationRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto")
        };
        operationRow.Children.Add(new TextBlock
        {
            Text = "操作：",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 0, 6, 0)
        });
        Grid.SetColumn(operationRow.Children[^1], 0);
        operationRow.Children.Add(_operationComboBox);
        Grid.SetColumn(_operationComboBox, 1);
        _operationComboBox.Items.Add(new ComboBoxItem { Content = "切换当前状态", Tag = PluginToggleOperation.Toggle });
        _operationComboBox.Items.Add(new ComboBoxItem { Content = "强制启用", Tag = PluginToggleOperation.Enable });
        _operationComboBox.Items.Add(new ComboBoxItem { Content = "强制禁用", Tag = PluginToggleOperation.Disable });
        _operationComboBox.SelectionChanged += (_, _) =>
        {
            if (_operationComboBox.SelectedItem is ComboBoxItem item && item.Tag is PluginToggleOperation op)
            {
                Settings.Operation = op;
            }
        };
        panel.Children.Add(operationRow);

        // 立即重启
        _restartImmediatelyCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (_restartImmediatelyCheckBox.IsChecked.HasValue)
            {
                Settings.RestartImmediately = _restartImmediatelyCheckBox.IsChecked.Value;
                _quietRestartCheckBox.IsEnabled = Settings.RestartImmediately;
            }
        };
        panel.Children.Add(_restartImmediatelyCheckBox);

        // 静默重启
        _quietRestartCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (_quietRestartCheckBox.IsChecked.HasValue)
            {
                Settings.QuietRestart = _quietRestartCheckBox.IsChecked.Value;
            }
        };
        panel.Children.Add(_quietRestartCheckBox);

        // 信息提示
        panel.Children.Add(_infoTextBlock);

        Content = panel;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        RefreshPluginList();
        RestoreSettings();
    }

    private void RefreshPluginList()
    {
        var pluginService = IAppHost.TryGetService<IPluginService>();
        if (pluginService == null)
        {
            _infoTextBlock.Text = "无法获取插件服务，请确保 ClassIsland 已正确加载。";
            _infoTextBlock.Foreground = Brushes.OrangeRed;
            _infoTextBlock.IsVisible = true;
            _plugins = new List<PluginInfo>();
            _pluginComboBox.ItemsSource = null;
            return;
        }

        _plugins = pluginService.LoadedPlugins.ToList();
        _pluginComboBox.ItemsSource = _plugins;

        if (_plugins.Count == 0)
        {
            _infoTextBlock.Text = "当前没有已加载的本地插件。";
            _infoTextBlock.Foreground = Brushes.Gray;
            _infoTextBlock.IsVisible = true;
        }
        else
        {
            _infoTextBlock.IsVisible = false;
        }

        RestoreSettings();
    }

    private void RestoreSettings()
    {
        if (Settings == null) return;

        // 恢复目标插件
        if (!string.IsNullOrWhiteSpace(Settings.PluginId))
        {
            var hit = _plugins.FirstOrDefault(p =>
                string.Equals(p.Manifest.Id, Settings.PluginId, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
            {
                _pluginComboBox.SelectedItem = hit;
            }
        }

        // 恢复操作类型
        var modeIndex = Settings.Operation switch
        {
            PluginToggleOperation.Enable => 1,
            PluginToggleOperation.Disable => 2,
            _ => 0
        };
        _operationComboBox.SelectedIndex = modeIndex;

        // 恢复复选框
        _restartImmediatelyCheckBox.IsChecked = Settings.RestartImmediately;
        _quietRestartCheckBox.IsChecked = Settings.QuietRestart;
        _quietRestartCheckBox.IsEnabled = Settings.RestartImmediately;
    }
}
