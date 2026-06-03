using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using SystemTools.ConfigHandlers;
using SystemTools.Services;
using SystemTools.Shared;

namespace SystemTools;

[HidePageTitle]
[SettingsPageInfo("systemtools.settings.floating", "悬浮窗编辑", "\uEA37", "\uEA37")]
public partial class FloatingWindowEditorSettingsPage : SettingsPageBase
{
    public FloatingWindowEditorSettingsPage()
    {
        if (GlobalConstants.MainConfig == null)
            GlobalConstants.MainConfig = new MainConfigHandler(GlobalConstants.PluginConfigFolder
                                                               ?? Path.Combine(
                                                                   Environment.GetFolderPath(Environment.SpecialFolder
                                                                       .LocalApplicationData), "ClassIsland", "Plugins",
                                                                   "SystemTools"));

        ViewModel = new SystemToolsSettingsViewModel(GlobalConstants.MainConfig,
            IAppHost.GetService<FloatingWindowService>());
        DataContext = this;
        InitializeComponent();

        DropHandler = new FloatingWindowDropHandler(this);

        ViewModel.RefreshFloatingWindowProfiles();
        ViewModel.RefreshFloatingTriggers();
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged += OnProfilePropertyChanged;
        ViewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
        ViewModel.ProfileChanged += OnViewModelProfileChanged;

        // 注册悬浮窗规则集变更监听
        if (ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged += OnHidingRulesPropertyChanged;
        }
    }

    public SystemToolsSettingsViewModel ViewModel { get; }
    public FloatingWindowDropHandler DropHandler { get; }

    private bool _isDisposed;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_isDisposed)
        {
            return;
        }

        // 页面卸载时确保所有配置（包括规则集）都已保存
        IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();

        ViewModel.CurrentFloatingWindowProfile.PropertyChanged -= OnProfilePropertyChanged;
        ViewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        ViewModel.ProfileChanged -= OnViewModelProfileChanged;

        // 注销悬浮窗规则集变更监听
        if (ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged -= OnHidingRulesPropertyChanged;
        }

        ViewModel.Dispose();
        _isDisposed = true;
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FloatingWindowProfile.FloatingWindowScale)
            or nameof(FloatingWindowProfile.FloatingWindowIconSize)
            or nameof(FloatingWindowProfile.FloatingWindowTextSize)
            or nameof(FloatingWindowProfile.FloatingWindowOpacity)
            or nameof(FloatingWindowProfile.FloatingWindowShadowEnabled)
            or nameof(FloatingWindowProfile.FloatingWindowLayer)
            or nameof(FloatingWindowProfile.FloatingWindowLayerRecheckMode)
            or nameof(FloatingWindowProfile.FloatingWindowHideOnRule)
            or nameof(FloatingWindowProfile.FloatingWindowDragHandleAlwaysVisible)
            or nameof(FloatingWindowProfile.FloatingWindowHorizontal))
        {
            IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        }
    }

    /// <summary>
    /// 重新注册 Profile 属性变更事件监听（切换方案后需要重新注册）
    /// </summary>
    public void ReattachProfilePropertyChanged()
    {
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged -= OnProfilePropertyChanged;
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged += OnProfilePropertyChanged;

        // 重新注册悬浮窗规则集变更监听
        if (ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged -= OnHidingRulesPropertyChanged;
            hidingRules.PropertyChanged += OnHidingRulesPropertyChanged;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainConfigData.FloatingWindowTheme))
        {
            GlobalConstants.MainConfig?.Save();
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        }
    }

    private void OnViewModelProfileChanged(object? sender, EventArgs e)
    {
        ReattachProfilePropertyChanged();
    }

    private void OnHidingRulesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
    }

    private void OnFloatingWindowVisibleToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }

        var service = IAppHost.GetService<FloatingWindowService>();
        var profile = ViewModel.CurrentFloatingWindowProfile;

        // 没有可用按钮时强制隐藏
        var shouldShow = toggle.IsChecked == true && service.Entries.Count > 0;
        profile.ShowFloatingWindow = shouldShow;

        // 同步 ToggleSwitch 状态（可能被强制隐藏）
        if (toggle.IsChecked != shouldShow)
        {
            toggle.IsChecked = shouldShow;
        }

        service.ProfileManager.SaveProfile();
        service.UpdateWindowState();
    }

    private void OnFloatingWindowProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not string profileName)
        {
            return;
        }

        ViewModel.SwitchFloatingWindowProfile(profileName);
    }

    private void OnToggleFloatingWindowProfileClick(object? sender, RoutedEventArgs e)
    {
        IAppHost.GetService<FloatingWindowService>().ToggleWindowProfile();
        ViewModel.RefreshFloatingWindowProfiles();
        ViewModel.RefreshFloatingTriggers();
    }

    private void OnAddFloatingWindowProfileClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddFloatingWindowProfile();
    }

    private void OnRemoveCurrentProfileClick(object? sender, RoutedEventArgs e)
    {
        var currentName = ViewModel.SelectedFloatingWindowProfile;
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return;
        }

        ViewModel.RemoveFloatingWindowProfile(currentName);
    }

    private void OnInsertRowBelowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        var row = control.GetVisualAncestors()
            .OfType<Border>()
            .Select(b => b.DataContext)
            .OfType<FloatingTriggerRow>()
            .FirstOrDefault();

        if (row == null)
        {
            return;
        }

        var index = ViewModel.FloatingTriggerRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        ViewModel.InsertFloatingTriggerRow(index + 1);
    }

    private void OnAddFloatingTriggerRowClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddFloatingTriggerRow();
    }

    private void OnRemoveFloatingTriggerRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerRow row })
        {
            return;
        }

        if (ViewModel.FloatingTriggerRows.Count <= 1)
        {
            return;
        }

        _ = ViewModel.RemoveFloatingTriggerRow(row);
    }

    private void OnRemoveTriggerFromRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string buttonId)
        {
            return;
        }

        ViewModel.RemoveTriggerToPool(buttonId);
    }

    // ===== 选中状态处理 =====

    private void OnRowSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is FloatingTriggerRow row)
        {
            ViewModel.SelectedFloatingTriggerRow = row;
        }
    }

    private void OnButtonSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is FloatingTriggerItem item)
        {
            ViewModel.SelectedFloatingTriggerItem = item;
        }
    }

    private void OnAvailableItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is FloatingTriggerItem item)
        {
            // 点击组件库项：添加到第一行末尾
            if (ViewModel.FloatingTriggerRows.Count == 0)
            {
                ViewModel.AddFloatingTriggerRow();
            }
            ViewModel.AddTriggerFromPool(item.ButtonId, 0, ViewModel.FloatingTriggerRows[0].Buttons.Count);

            // 清除选中状态
            listBox.SelectedItem = null;
        }
    }

    // ===== 规则集 Drawer（参照 ClassIsland） =====

    private void ButtonOpenButtonRuleset_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindResource("RulesetControl") is not ClassIsland.Core.Controls.Ruleset.RulesetControl control
            || ViewModel.SelectedFloatingTriggerItem == null)
            return;
        control.Ruleset = ViewModel.SelectedFloatingTriggerItem.Config.HidingRules;
        OpenDrawer("RulesetControl");
    }

    private void ButtonOpenRowRuleset_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindResource("RulesetControl") is not ClassIsland.Core.Controls.Ruleset.RulesetControl control
            || ViewModel.SelectedFloatingTriggerRow == null)
            return;
        control.Ruleset = ViewModel.SelectedFloatingTriggerRow.RowRuleset.HidingRules;
        OpenDrawer("RulesetControl");
    }

    private void ButtonOpenFloatingWindowRuleset_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindResource("RulesetControl") is not ClassIsland.Core.Controls.Ruleset.RulesetControl control)
            return;
        control.Ruleset = ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules;
        OpenDrawer("RulesetControl");
    }
}
